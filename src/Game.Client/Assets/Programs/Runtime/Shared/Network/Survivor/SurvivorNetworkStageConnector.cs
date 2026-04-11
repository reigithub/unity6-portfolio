using System;
using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using Game.Shared.Environment;
using Game.Shared.Network.Fusion;
using Game.Shared.Services;
using Game.Shared.Signals.Survivor;
using Game.Shared.Unity.Server;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// ISurvivorNetworkStageConnector の Fusion 2 実装。
    /// Fusion NetworkRunner を生成し、Host/Client/Server セッションを管理する。
    /// </summary>
    public class SurvivorNetworkStageConnector : ISurvivorNetworkStageConnector
    {
        [Inject] private readonly IObjectResolver _resolver;
        [Inject] private readonly IAddressableAssetService _assetService;
        [Inject] private readonly IFusionRunnerService _runnerService;
        [Inject] private readonly IUnityServerSessionConfig _sessionConfig;
        [Inject] private readonly IUnityServerAuthProviderFactory _authProviderFactory;
        [Inject] private readonly IPublisher<SurvivorSignals.Session.AllPlayersReady> _allPlayersReadyPub;
        [Inject] private readonly IPublisher<SurvivorSignals.Session.GameStarted> _gameStartedPub;
        [Inject] private readonly IPublisher<SurvivorSignals.Session.AllPlayersDisconnected> _allPlayersDisconnectedPub;

        private const string GameStateAddress = "SurvivorFusionGameState";
        private const string PlayerAddress = "SurvivorFusionPlayer";
        private const string EnemyBatchSyncAddress = "SurvivorFusionEnemyBatchSync";

        private SurvivorFusionRunner _runner;
        private SurvivorUnityServerSession _session;
        private GameMode _gameMode;
        private bool _isConnecting;

        // Addressables で読み込んだプレハブ（解放用に保持）
        private GameObject _gameStatePrefabAsset;
        private GameObject _playerPrefabAsset;
        private GameObject _enemyBatchSyncPrefabAsset;

        public bool IsConnected => _runner != null && _runner.Runner != null;

        public async UniTask StartHostAsync(int stageId)
        {
            if (_isConnecting || IsConnected) return;
            _isConnecting = true;

            try
            {
                _gameMode = GameMode.Host;
                var sessionName = _sessionConfig.SessionName;
                var expectedPlayers = _sessionConfig.MaxPlayerCount;

                await PreloadPrefabsAsync();
                EnsureRunner();
                CreateSession(expectedPlayers);

                var config = new FusionConnectionConfig
                {
                    GameMode = GameMode.Host,
                    SessionName = sessionName,
                    Address = NetAddress.Any(),
                    ConnectionToken = null,
                };

                var result = await _runner.StartAsync(config);
                if (!result.Ok)
                    throw new InvalidOperationException($"Fusion Host start failed: {result.ShutdownReason}");

                SpawnGameState(_runner.Runner);
                SpawnEnemyBatchSync(_runner.Runner);
                _runnerService.Initialize(_runner.Runner, _resolver);
                Debug.Log("[SurvivorFusionStageConnector] Host mode started");
            }
            finally
            {
                _isConnecting = false;
            }
        }

        public async UniTask ConnectAsync(string address, ushort port, int stageId, string sessionToken = "")
        {
            if (_isConnecting || IsConnected) return;
            _isConnecting = true;

            try
            {
                _gameMode = GameMode.Client;
                var sessionName = _sessionConfig.SessionName;

                EnsureRunner();

                // セッショントークン（Base64 文字列）をバイナリに変換して ConnectionToken として送信
                // サーバー側は MessagePack + HMAC-SHA256 バイナリ形式（~117B、128B 上限以内）
                byte[] connectionToken = null;
                if (!string.IsNullOrEmpty(sessionToken))
                {
                    connectionToken = Convert.FromBase64String(sessionToken);
                    if (connectionToken.Length > 128)
                    {
                        Debug.LogWarning($"[SurvivorFusionStageConnector] ConnectionToken {connectionToken.Length}B が 128B を超えています。トークンが Fusion に無視される可能性があります。");
                    }
                }

                var config = new FusionConnectionConfig
                {
                    GameMode = GameMode.Client,
                    SessionName = sessionName,
                    Address = NetAddress.CreateFromIpPort(address, port),
                    ConnectionToken = connectionToken,
                };

                var result = await _runner.StartAsync(config);
                if (!result.Ok)
                    throw new InvalidOperationException($"Fusion Client connect failed: {result.ShutdownReason}");

                _runnerService.Initialize(_runner.Runner, _resolver);
                Debug.Log($"[SurvivorFusionStageConnector] Connected to session: {sessionName}");
            }
            finally
            {
                _isConnecting = false;
            }
        }

        public async UniTask StartServerAsync(int stageId)
        {
            if (_isConnecting) return;
            _isConnecting = true;

            try
            {
                _gameMode = GameMode.Server;
                var sessionName = _sessionConfig.SessionName;
                var expectedPlayers = _sessionConfig.MaxPlayerCount;

                await PreloadPrefabsAsync();
                EnsureRunner();
                CreateSession(expectedPlayers);

                var serverPort = _sessionConfig.ServerPort;

                // 環境変数 PUBLIC_ADDRESS から公開アドレスを取得（GCE/NAT対応）
                NetAddress? publicAddress = null;
                if (EnvVarHelper.TryGet(EnvVarKeys.PublicAddress, out var publicIp))
                {
                    publicAddress = NetAddress.CreateFromIpPort(publicIp, serverPort);
                }

                var config = new FusionConnectionConfig
                {
                    GameMode = GameMode.Server,
                    SessionName = sessionName,
                    Address = NetAddress.Any(serverPort),
                    CustomPublicAddress = publicAddress,
                    ConnectionToken = null,
                };

                var result = await _runner.StartAsync(config);
                if (!result.Ok)
                    throw new InvalidOperationException($"Fusion Server start failed: {result.ShutdownReason}");

                SpawnGameState(_runner.Runner);
                SpawnEnemyBatchSync(_runner.Runner);
                _runnerService.Initialize(_runner.Runner, _resolver);
                Debug.Log("[SurvivorFusionStageConnector] Server-only mode started");
            }
            finally
            {
                _isConnecting = false;
            }
        }

        public async UniTask DisconnectAsync()
        {
            if (_runner != null)
            {
                if (_runner.Runner != null)
                {
                    await _runner.Runner.Shutdown();
                }

                if (_runner != null)
                {
                    UnityEngine.Object.Destroy(_runner.gameObject);
                    _runner = null;
                }
            }

            _session = null;
            ReleasePrefabs();
            _runnerService.Clear();
            Debug.Log("[SurvivorFusionStageConnector] Disconnected");
        }

        /// <summary>同期版（互換性維持）。内部でDisconnectAsyncをFire-and-Forgetで実行。</summary>
        public void Disconnect() => DisconnectAsync().Forget();

        public void Dispose() => DisconnectAsync().Forget();

        // =====================================================================
        //  プレハブ読み込み（Addressables）
        // =====================================================================

        private async UniTask PreloadPrefabsAsync()
        {
            _gameStatePrefabAsset = await LoadPrefabIfNeededAsync(GameStateAddress, _gameStatePrefabAsset);
            _playerPrefabAsset = await LoadPrefabIfNeededAsync(PlayerAddress, _playerPrefabAsset);
            _enemyBatchSyncPrefabAsset = await LoadPrefabIfNeededAsync(EnemyBatchSyncAddress, _enemyBatchSyncPrefabAsset);
        }

        private async UniTask<GameObject> LoadPrefabIfNeededAsync(string address, GameObject current)
        {
            if (current != null) return current;
            var loaded = await _assetService.LoadAssetAsync<GameObject>(address);
            if (loaded == null)
                Debug.LogError($"[SurvivorFusionStageConnector] Failed to load: {address}");
            return loaded;
        }

        private void ReleasePrefabs()
        {
            if (_gameStatePrefabAsset != null)
            {
                _assetService.ReleaseAsset(_gameStatePrefabAsset);
                _gameStatePrefabAsset = null;
            }
            if (_playerPrefabAsset != null)
            {
                _assetService.ReleaseAsset(_playerPrefabAsset);
                _playerPrefabAsset = null;
            }
            if (_enemyBatchSyncPrefabAsset != null)
            {
                _assetService.ReleaseAsset(_enemyBatchSyncPrefabAsset);
                _enemyBatchSyncPrefabAsset = null;
            }
        }

        // =====================================================================
        //  内部
        // =====================================================================

        private void EnsureRunner()
        {
            if (_runner != null) return;

            var go = new GameObject("[FusionRunner]");
            _runner = go.AddComponent<SurvivorFusionRunner>();
            _runner.Initialize();
            _runner.Resolver = _resolver;
            _runner.RunnerService = _runnerService;
            _runner.OnShutdownCallback = OnRunnerShutdown;

            // 認証プロバイダを設定（Client 側は NullFactory が null を返すため認証スキップ）
            var authProvider = _authProviderFactory.Create();
            if (authProvider != null)
                _runner.AuthProvider = authProvider;
        }

        private void CreateSession(int expectedPlayerCount)
        {
            NetworkObject playerPrefab = null;
            if (_playerPrefabAsset != null)
                playerPrefab = _playerPrefabAsset.GetComponent<NetworkObject>();

            _session = new SurvivorUnityServerSession(
                _runnerService,
                _allPlayersReadyPub,
                _gameStartedPub,
                _allPlayersDisconnectedPub,
                expectedPlayerCount,
                playerPrefab);

            _runner.Session = _session;
        }

        private void SpawnGameState(NetworkRunner runner)
        {
            if (_gameStatePrefabAsset == null) return;

            var prefab = _gameStatePrefabAsset.GetComponent<NetworkObject>();
            if (prefab == null) return;

            runner.Spawn(prefab);
            Debug.Log("[SurvivorFusionStageConnector] GameState spawned");
        }

        private void SpawnEnemyBatchSync(NetworkRunner runner)
        {
            if (_enemyBatchSyncPrefabAsset == null) return;

            var prefab = _enemyBatchSyncPrefabAsset.GetComponent<NetworkObject>();
            if (prefab == null) return;

            runner.Spawn(prefab);
            Debug.Log("[SurvivorFusionStageConnector] EnemyBatchSync spawned");
        }

        private void OnRunnerShutdown(ShutdownReason reason)
        {
            _runnerService.Clear();
            _runnerService.RaiseClientDisconnected();
            _session = null;
        }
    }
}
