using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using VContainer;

namespace Game.Shared.Network.Fusion
{
    /// <summary>
    /// Fusion 2 NetworkRunner のホスト MonoBehaviour。
    /// INetworkRunnerCallbacks を実装し、セッション管理へ委譲する。
    /// </summary>
    public class SurvivorFusionRunner : MonoBehaviour, INetworkRunnerCallbacks
    {
        public static SurvivorFusionRunner Instance { get; private set; }
        public NetworkRunner Runner { get; private set; }

        /// <summary>セッション管理（Host/Server 時に設定）</summary>
        internal SurvivorFusionServerSession Session { get; set; }

        /// <summary>Shutdown 時の通知</summary>
        internal Action<ShutdownReason> OnShutdownCallback { get; set; }

        /// <summary>入力収集デリゲート（SurvivorFusionPlayer が設定）</summary>
        internal Func<PlayerNetworkInput> InputProvider { get; set; }

        /// <summary>VContainer リゾルバ（クライアント側レプリカの DI 注入用）</summary>
        internal IObjectResolver Resolver { get; set; }

        public void Initialize()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Fusion セッションを開始する。
        /// </summary>
        public async Cysharp.Threading.Tasks.UniTask<StartGameResult> StartAsync(
            GameMode gameMode, string sessionName)
        {
            Runner = gameObject.AddComponent<NetworkRunner>();
            Runner.ProvideInput = gameMode != GameMode.Server;

            var sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();

            var objectProvider = gameObject.AddComponent<VContainerNetworkObjectProvider>();
            objectProvider.SetResolver(Resolver);

            var result = await Runner.StartGame(new StartGameArgs
            {
                GameMode = gameMode,
                SessionName = sessionName,
                SceneManager = sceneManager,
                ObjectProvider = objectProvider,
            });

            if (result.Ok)
            {
                Debug.Log($"[SurvivorFusionRunner] Session started: mode={gameMode}, session={sessionName}");
            }
            else
            {
                Debug.LogError($"[SurvivorFusionRunner] Failed to start: {result.ShutdownReason}");
            }

            return result;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
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
            NetworkModeHelper.RaiseClientDisconnected();
        }

        // --- 未使用コールバック（最小実装） ---
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
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
