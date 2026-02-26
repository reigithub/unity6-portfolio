#if UNITY_SERVER
using UnityEngine;

namespace Game.Shared.DedicatedServer
{
    /// <summary>
    /// Dedicated Server 起動時の初期化処理
    /// Phase 3 で IPC ブリッジ初期化を追加予定
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
        }
    }
}
#endif
