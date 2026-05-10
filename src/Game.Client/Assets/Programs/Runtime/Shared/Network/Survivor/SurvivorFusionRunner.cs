using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Addons.Physics;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using Game.Shared.Network.Fusion;
using Game.Shared.Signals.Survivor;
using Game.Shared.Unity.Server;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// Fusion 2 NetworkRunner を保持する MonoBehaviour コンテナ。
    /// INetworkRunnerCallbacks を実装し、プレイヤー接続管理・セッション通知を直接担当する。
    /// （ここでいう「保持」は GameObject に NetworkRunner を同居させる意味で、
    /// Fusion GameMode.Host とは別概念）
    /// </summary>
    public class SurvivorFusionRunner : MonoBehaviour, INetworkRunnerCallbacks
    {
        public NetworkRunner Runner { get; private set; }

        /// <summary>Shutdown 時の通知</summary>
        internal Action<ShutdownReason> OnShutdownCallback { get; set; }

        /// <summary>入力収集デリゲート（SurvivorFusionPlayer が設定）</summary>
        internal Func<SurvivorPlayerNetworkInput> InputProvider { get; set; }

        /// <summary>VContainer リゾルバ（クライアント側レプリカの DI 注入用）</summary>
        internal IObjectResolver Resolver { get; set; }

        /// <summary>接続認証プロバイダ。Server モード時に設定すると OnConnectRequest で検証する。</summary>
        internal IUnityServerAuthProvider AuthProvider { get; set; }

        // --- VContainer フィールドインジェクション ---
        [Inject] private IPublisher<SurvivorSignals.Session.GameStarted> _gameStartedPub;
        [Inject] private IPublisher<SurvivorSignals.Session.AllPlayersDisconnected> _allPlayersDisconnectedPub;
        [Inject] private IGameSessionConfig _sessionConfig;
        [Inject] private IFusionRunnerService _runnerService;

        // --- セッション管理フィールド（Host/Server 時のみ使用） ---
        private readonly HashSet<PlayerRef> _connectedPlayers = new();
        private bool _allPlayersNotified;
        private NetworkObject _playerPrefab;
        private bool _sessionEnabled;

        public void Initialize()
        {
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Host/Server モード時のセッション設定。StartAsync の前に呼ぶこと。
        /// この呼び出しによりセッション系コールバック処理が有効化される (_sessionEnabled = true)。
        /// SpawnConnectedPlayers はステージシーンロード後に呼ぶこと。
        /// </summary>
        /// <param name="playerPrefab">スポーンするプレイヤーの NetworkObject プレハブ</param>
        public void Configure(NetworkObject playerPrefab)
        {
            _playerPrefab = playerPrefab;
            _sessionEnabled = true;
        }

        // 診断: 5 秒間の FPS / 最大フレーム時間 / OnInput 呼出回数を集計
        private const float DiagSummaryInterval = 5f;
        private int _diagFrameCount;
        private int _diagOnInputCount;
        private float _diagMaxFrameTime;
        private float _diagLastSummaryTime;

        protected void Update()
        {
            if (Runner == null || !Runner.IsRunning) return;

            _diagFrameCount++;
            var dt = Time.unscaledDeltaTime;
            if (dt > _diagMaxFrameTime) _diagMaxFrameTime = dt;

            var now = Time.unscaledTime;
            var elapsed = now - _diagLastSummaryTime;
            if (elapsed >= DiagSummaryInterval)
            {
                var fps = _diagFrameCount / elapsed;
                Debug.Log($"[FusionRunner DIAG] FPS={fps:F1}, MaxFrameTime={_diagMaxFrameTime * 1000f:F2}ms, OnInputCalls={_diagOnInputCount} (window={elapsed:F1}s, mode={Runner.GameMode})");
                _diagFrameCount = 0;
                _diagOnInputCount = 0;
                _diagMaxFrameTime = 0f;
                _diagLastSummaryTime = now;
            }
        }

        /// <summary>
        /// Fusion セッションを開始する。
        /// FusionConnectionConfig に必要なパラメータをすべてまとめて受け取る。
        /// </summary>
        /// <param name="config">接続設定（GameMode / SessionName / Address / ConnectionToken 等）</param>
        public async UniTask<StartGameResult> StartAsync(FusionConnectionConfig config)
        {
            Runner = gameObject.AddComponent<NetworkRunner>();
            Runner.ProvideInput = config.GameMode != GameMode.Server;

            // Physics Addon: KCC は独自の物理クエリを使用するため Physics.Simulate() は不要。
            // プロジェクタイルは SphereCast（即時クエリ）でヒット検出するため SyncTransforms で十分。
            var physicsSimulation = gameObject.AddComponent<RunnerSimulatePhysics3D>();
            physicsSimulation.ClientPhysicsSimulation = ClientPhysicsSimulation.SyncTransforms;

            var sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();

            var objectProvider = gameObject.AddComponent<VContainerNetworkObjectProvider>();
            objectProvider.SetResolver(Resolver);

            var startGameArgs = new StartGameArgs
            {
                GameMode = config.GameMode,
                SessionName = config.SessionName,
                Address = config.Address,
                CustomPublicAddress = config.CustomPublicAddress,
                ConnectionToken = config.ConnectionToken,
                SceneManager = sceneManager,
                ObjectProvider = objectProvider,
            };

            // P2P 用 region 動的指定: PhotonAppSettings.Global.AppSettings.GetCopy() + FixedRegion 上書き
            // 公式パターン (https://doc.photonengine.com/fusion/current/manual/connection-and-matchmaking/regions)。
            // null/空 の場合は CustomPhotonAppSettings 未指定 → PhotonAppSettings.asset の FixedRegion へフォールバック。
            if (!string.IsNullOrEmpty(config.PhotonRegion))
            {
                var appSettings = PhotonAppSettings.Global.AppSettings;
                if (appSettings != null)
                {
                    var customAppSettings = appSettings.GetCopy();
                    customAppSettings.FixedRegion = config.PhotonRegion.ToLowerInvariant();
                    startGameArgs.CustomPhotonAppSettings = customAppSettings;
                    Debug.Log($"[SurvivorFusionRunner] Custom region set: {customAppSettings.FixedRegion}");
                }
                else
                {
                    Debug.LogWarning("[SurvivorFusionRunner] PhotonAppSettings.Global.AppSettings is null, falling back to default region");
                }
            }

            var result = await Runner.StartGame(startGameArgs);

            if (result.Ok)
            {
                Debug.Log($"[SurvivorFusionRunner] Session started: mode={config.GameMode}, session={config.SessionName}, address={config.Address}");
            }
            else
            {
                Debug.LogError($"[SurvivorFusionRunner] Failed to start: {result.ShutdownReason}, ErrorMessage: {result.ErrorMessage}");
                if (result.StackTrace != null) Debug.LogError($"[SurvivorFusionRunner] StackTrace: {result.StackTrace}");
            }

            return result;
        }

        /// <summary>
        /// 接続中の全プレイヤーを指定位置にスポーンする。
        /// ステージシーンロード後に呼ぶ。
        /// </summary>
        public void SpawnConnectedPlayers(Vector3 position, Quaternion rotation)
        {
            if (_playerPrefab == null)
            {
                Debug.LogError("[SurvivorFusionRunner] Player prefab is null!");
                return;
            }

            foreach (var player in _connectedPlayers)
            {
                if (Runner.GetPlayerObject(player) != null) continue;

                var playerObj = Runner.Spawn(_playerPrefab, position, rotation, inputAuthority: player);
                Runner.SetPlayerObject(player, playerObj);
                Debug.Log($"[SurvivorFusionRunner] Spawned player {player} at {position}");
            }
        }

        // =====================================================================
        //  INetworkRunnerCallbacks
        // =====================================================================

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[SurvivorFusionRunner] Player joined: {player}");

            if (!_sessionEnabled) return;

            _connectedPlayers.Add(player);
            Debug.Log($"[SurvivorFusionRunner] Player tracked: {player} ({_connectedPlayers.Count}/{_sessionConfig.PlayerCount})");

            // Spawn はステージシーンロード後に SpawnConnectedPlayers() で行う

            if (_connectedPlayers.Count >= _sessionConfig.PlayerCount && !_allPlayersNotified)
            {
                _allPlayersNotified = true;
                NotifyAllPlayersReadyAsync().Forget();
            }
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[SurvivorFusionRunner] Player left: {player}");

            if (!_sessionEnabled) return;

            // 切断時の Pause クリーンアップ (LevelUp 中切断で全体停止が永続化するのを防ぐ)
            if (_runnerService != null && _runnerService.TryGet<SurvivorFusionGameState>(out var gs))
            {
                gs.OnPlayerDisconnectedCleanup(player);
            }

            // プレイヤー NetworkObject をデスポーン
            var playerObj = runner.GetPlayerObject(player);
            if (playerObj != null)
            {
                runner.Despawn(playerObj);
            }

            _connectedPlayers.Remove(player);
            Debug.Log($"[SurvivorFusionRunner] Player removed: {player} ({_connectedPlayers.Count} remaining)");

            if (_connectedPlayers.Count <= 0 && _allPlayersNotified)
            {
                // リトライ時に再接続を受け入れるためリセット
                _allPlayersNotified = false;

                Debug.Log("[SurvivorFusionRunner] All players disconnected");
                _allPlayersDisconnectedPub?.Publish(new SurvivorSignals.Session.AllPlayersDisconnected());
            }
        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            _diagOnInputCount++;
            if (InputProvider != null)
            {
                input.Set(InputProvider());
            }
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Debug.Log($"[SurvivorFusionRunner] Shutdown: {shutdownReason}");
            OnShutdownCallback?.Invoke(shutdownReason);
        }

        public void OnConnectedToServer(NetworkRunner runner)
        {
            Debug.Log("[SurvivorFusionRunner] Connected to server");
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            Debug.Log($"[SurvivorFusionRunner] Disconnected from server: {reason}");
            _runnerService?.RaiseClientDisconnected();
        }

        // --- 未使用コールバック（最小実装） ---
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

        /// <summary>
        /// クライアントからの接続要求。AuthProvider が設定されている場合は ConnectionToken を検証する。
        /// </summary>
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            if (AuthProvider == null)
            {
                request.Accept();
                return;
            }

            if (AuthProvider.ValidateConnectionToken(token))
            {
                request.Accept();
            }
            else
            {
                Debug.LogWarning("[SurvivorFusionRunner] Connection refused: invalid token");
                request.Refuse();
            }
        }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            Debug.LogError($"[SurvivorFusionRunner] Connect failed: {reason}");
        }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        // =====================================================================
        //  Private
        // =====================================================================

        /// <summary>
        /// AllPlayersReady を1フレーム遅延で発火する。
        /// SurvivorStageConnectScene の WaitForAllPlayersReadyAsync() が購読登録を完了した後に届くようにする。
        /// </summary>
        private async UniTaskVoid NotifyAllPlayersReadyAsync()
        {
            await UniTask.NextFrame();

            // ゲーム状態にプレイヤー数を設定（全滅判定用）
            if (_runnerService.TryGet<SurvivorFusionGameState>(out var gs))
            {
                gs.SetTotalPlayerCount(_sessionConfig.PlayerCount);

                // RPC で全クライアントに通知（MPPM 等では別 DI コンテナのため MessagePipe だけでは届かない）
                gs.RpcNotifyAllPlayersReady();
            }

            // サーバーローカルの GameStarted シグナル
            _gameStartedPub?.Publish(new SurvivorSignals.Session.GameStarted(Time.time));

            Debug.Log("[SurvivorFusionRunner] AllPlayersReady (RPC) + GameStarted published");
        }
    }
}
