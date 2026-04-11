using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Addons.Physics;
using Fusion.Sockets;
using Game.Shared.Network.Fusion;
using Game.Shared.Unity.Server;
using UnityEngine;
using VContainer;

namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// Fusion 2 NetworkRunner のホスト MonoBehaviour。
    /// INetworkRunnerCallbacks を実装し、セッション管理へ委譲する。
    /// </summary>
    public class SurvivorFusionRunner : MonoBehaviour, INetworkRunnerCallbacks
    {
        public NetworkRunner Runner { get; private set; }

        /// <summary>セッション管理（Host/Server 時に設定）</summary>
        internal SurvivorUnityServerSession Session { get; set; }

        /// <summary>Shutdown 時の通知</summary>
        internal Action<ShutdownReason> OnShutdownCallback { get; set; }

        /// <summary>入力収集デリゲート（SurvivorFusionPlayer が設定）</summary>
        internal Func<SurvivorPlayerNetworkInput> InputProvider { get; set; }

        /// <summary>VContainer リゾルバ（クライアント側レプリカの DI 注入用）</summary>
        internal IObjectResolver Resolver { get; set; }

        /// <summary>IFusionRunnerService（SurvivorFusionStageConnector が設定）</summary>
        internal IFusionRunnerService RunnerService { get; set; }

        /// <summary>接続認証プロバイダ。Server モード時に設定すると OnConnectRequest で検証する。</summary>
        internal IUnityServerAuthProvider AuthProvider { get; set; }

        public void Initialize()
        {
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Fusion セッションを開始する。
        /// FusionConnectionConfig に必要なパラメータをすべてまとめて受け取る。
        /// </summary>
        /// <param name="config">接続設定（GameMode / SessionName / Address / ConnectionToken 等）</param>
        public async Cysharp.Threading.Tasks.UniTask<StartGameResult> StartAsync(FusionConnectionConfig config)
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

            var result = await Runner.StartGame(new StartGameArgs
            {
                GameMode = config.GameMode,
                SessionName = config.SessionName,
                Address = config.Address,
                CustomPublicAddress = config.CustomPublicAddress,
                ConnectionToken = config.ConnectionToken,
                SceneManager = sceneManager,
                ObjectProvider = objectProvider,
            });

            if (result.Ok)
            {
                Debug.Log($"[SurvivorFusionRunner] Session started: mode={config.GameMode}, session={config.SessionName}, address={config.Address}");
            }
            else
            {
                Debug.LogError($"[SurvivorFusionRunner] Failed to start: {result.ShutdownReason}");
            }

            return result;
        }

        /// <summary>
        /// 接続中の全プレイヤーを指定位置にスポーンする。
        /// ステージシーンロード後に呼ぶ。
        /// </summary>
        public void SpawnConnectedPlayers(Vector3 position, Quaternion rotation)
        {
            Session?.SpawnConnectedPlayers(Runner, position, rotation);
        }

        // =====================================================================
        //  INetworkRunnerCallbacks
        // =====================================================================

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[SurvivorFusionRunner] Player joined: {player}");
            Session?.OnPlayerJoined(runner, player);
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[SurvivorFusionRunner] Player left: {player}");
            Session?.OnPlayerLeft(runner, player);
        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
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
            RunnerService?.RaiseClientDisconnected();
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
    }
}
