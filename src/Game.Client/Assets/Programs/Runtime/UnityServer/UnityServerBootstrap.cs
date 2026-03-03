using System;
using System.Text;
using Game.Shared.Network.Survivor;
using Game.Shared.Environment;
using UnityEngine;

namespace Game.Unity.Server
{
    /// <summary>
    /// Dedicated Server 起動時の初期化処理。
    /// Mirror サーバーを自動起動し、クライアント接続を受け入れる。
    /// </summary>
    public static class UnityServerBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
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

            // --- コマンドライン引数解析 ---
            ushort port = UnityServerNetworkManager.ParsePort();
            string secret = ParseSecret();
            int playerCount = ParsePlayerCount();

            Debug.Log($"[ServerBootstrap] Starting Mirror Server on port {port}, players={playerCount}...");

            // HMAC 共有シークレット設定（MP モード）
            if (!string.IsNullOrEmpty(secret))
            {
                SurvivorNetworkAuthenticator.SharedSecret = Encoding.UTF8.GetBytes(secret);
                Debug.Log("[ServerBootstrap] SharedSecret configured for HMAC token verification");
            }

            // --- Mirror Server 起動 ---
            var serverGo = new GameObject("[ServerNetworkManager]");
            UnityEngine.Object.DontDestroyOnLoad(serverGo);

            // インフラ起動（NM + Transport 作成 → 次フレームで StartServer）
            var serverNm = serverGo.AddComponent<UnityServerNetworkManager>();
            serverNm.Initialize(port);

            // Survivor セッション開始（クライアント接続受け入れ準備）
            var session = serverGo.AddComponent<SurvivorUnityServerSession>();
            session.StartSession(playerCount);
        }

        /// <summary>
        /// コマンドライン引数から --secret を解析。
        /// </summary>
        private static string ParseSecret()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--secret")
                    return args[i + 1];
            }
            // 環境変数フォールバック（GCP / SP Local からの注入）
            return System.Environment.GetEnvironmentVariable(EnvVarKeys.UnityServerAuthSecretKey);
        }

        /// <summary>
        /// コマンドライン引数から --players を解析。デフォルト 1。
        /// </summary>
        private static int ParsePlayerCount()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--players" && int.TryParse(args[i + 1], out int count))
                    return count;
            }
            return 1;
        }
    }
}
