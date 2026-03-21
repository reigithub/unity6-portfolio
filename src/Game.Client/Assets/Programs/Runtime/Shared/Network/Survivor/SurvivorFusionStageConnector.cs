using System;
using Cysharp.Threading.Tasks;
using Fusion;
using Game.Shared.Network.Fusion;
using Game.Shared.Services;
using Game.Shared.Signals.Survivor;
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
    public class SurvivorFusionStageConnector : ISurvivorNetworkStageConnector
    {
        [Inject] private readonly IObjectResolver _resolver;
        [Inject] private readonly IAddressableAssetService _assetService;
        [Inject] private readonly IFusionRunnerService _runnerService;
        [Inject] private readonly IPublisher<SurvivorSignals.Session.AllPlayersReady> _allPlayersReadyPub;
        [Inject] private readonly IPublisher<SurvivorSignals.Session.GameStarted> _gameStartedPub;
        [Inject] private readonly IPublisher<SurvivorSignals.Session.AllPlayersDisconnected> _allPlayersDisconnectedPub;

        private const string GameStateAddress = "SurvivorFusionGameState";
        private const string PlayerAddress = "SurvivorFusionPlayer";
        private const string EnemyBatchSyncAddress = "SurvivorFusionEnemyBatchSync";

        private SurvivorFusionRunner _runner;
        private SurvivorFusionServerSession _session;
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
                var sessionName = SurvivorNetworkMatchConnector.MatchId;
                var expectedPlayers = SurvivorNetworkMatchConnector.ExpectedPlayerCount;

                await PreloadPrefabsAsync();
                EnsureRunner();
                CreateSession(expectedPlayers);

                var result = await _runner.StartAsync(GameMode.Host, sessionName);
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
                var sessionName = SurvivorNetworkMatchConnector.MatchId;

                EnsureRunner();

                var result = await _runner.StartAsync(GameMode.Client, sessionName);
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
                var sessionName = SurvivorNetworkMatchConnector.MatchId;
                var expectedPlayers = SurvivorNetworkMatchConnector.ExpectedPlayerCount;

                await PreloadPrefabsAsync();
                EnsureRunner();
                CreateSession(expectedPlayers);

                var result = await _runner.StartAsync(GameMode.Server, sessionName);
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

        public void Disconnect()
        {
            if (_runner != null)
            {
                if (_runner.Runner != null)
                {
                    _runner.Runner.Shutdown();
                }
                UnityEngine.Object.Destroy(_runner.gameObject);
                _runner = null;
            }

            _session = null;
            ReleasePrefabs();
            _runnerService.Clear();
            Debug.Log("[SurvivorFusionStageConnector] Disconnected");
        }

        public void Dispose() => Disconnect();

        // =====================================================================
        //  プレハブ読み込み（Addressables）
        // =====================================================================

        private async UniTask PreloadPrefabsAsync()
        {
            if (_gameStatePrefabAsset == null)
            {
                _gameStatePrefabAsset = await _assetService.LoadAssetAsync<GameObject>(GameStateAddress);
                if (_gameStatePrefabAsset == null)
                    Debug.LogError($"[SurvivorFusionStageConnector] Failed to load: {GameStateAddress}");
            }

            if (_playerPrefabAsset == null)
            {
                _playerPrefabAsset = await _assetService.LoadAssetAsync<GameObject>(PlayerAddress);
                if (_playerPrefabAsset == null)
                    Debug.LogError($"[SurvivorFusionStageConnector] Failed to load: {PlayerAddress}");
            }

            if (_enemyBatchSyncPrefabAsset == null)
            {
                _enemyBatchSyncPrefabAsset = await _assetService.LoadAssetAsync<GameObject>(EnemyBatchSyncAddress);
                if (_enemyBatchSyncPrefabAsset == null)
                    Debug.LogError($"[SurvivorFusionStageConnector] Failed to load: {EnemyBatchSyncAddress}");
            }
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
        }

        private void CreateSession(int expectedPlayerCount)
        {
            NetworkObject playerPrefab = null;
            if (_playerPrefabAsset != null)
                playerPrefab = _playerPrefabAsset.GetComponent<NetworkObject>();

            _session = new SurvivorFusionServerSession(
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
