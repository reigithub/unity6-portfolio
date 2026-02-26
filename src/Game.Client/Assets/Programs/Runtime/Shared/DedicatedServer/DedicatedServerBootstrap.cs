#if UNITY_SERVER
using Game.Shared.DedicatedServer.Netcode;
using UnityEngine;

namespace Game.Shared.DedicatedServer
{
    /// <summary>
    /// Dedicated Server 起動時の初期化処理。
    /// NGO サーバーを自動起動し、クライアント接続を受け入れる。
    /// </summary>
    public static class DedicatedServerBootstrap
    {
        // [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            Debug.Log("[ServerBootstrap] ========================================");
            Debug.Log("[ServerBootstrap] Dedicated Server starting...");
            Debug.Log($"[ServerBootstrap] BatchMode: {Application.isBatchMode}");
            Debug.Log($"[ServerBootstrap] Platform: {Application.platform}");
            Debug.Log($"[ServerBootstrap] Unity Version: {Application.unityVersion}");
            Debug.Log($"[ServerBootstrap] Product Version: {Application.version}");
            Debug.Log("[ServerBootstrap] ========================================");

            // サーバー向けフレームレート設定
            Application.targetFrameRate = 60;

            // スクリーンスリープ無効化
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // --- NGO Server 起動 ---
            ushort port = ServerNetworkManager.ParsePort();
            Debug.Log($"[ServerBootstrap] Starting NGO Server on port {port}...");

            var serverGo = new GameObject("[ServerNetworkManager]");
            Object.DontDestroyOnLoad(serverGo);
            var serverNm = serverGo.AddComponent<ServerNetworkManager>();
            serverNm.Initialize(port);
        }
    }
}
#endif
