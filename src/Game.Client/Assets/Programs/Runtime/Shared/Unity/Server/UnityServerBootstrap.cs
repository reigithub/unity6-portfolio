using System.Text;
using Game.Shared.Network.Survivor;
using UnityEngine;

namespace Game.Shared.Unity.Server
{
    /// <summary>
    /// Dedicated Server 起動時の初期化処理。
    /// Fusion Server モードで起動し、クライアント接続を受け入れる。
    /// GameRuntimeInitializer からサーバーモード時に明示的に呼び出される。
    /// </summary>
    public static class UnityServerBootstrap
    {
        private static TcpHealthProbe _healthProbe;

        /// <summary>
        /// HMAC 認証用シークレットキー。
        /// --secret 引数または UNITY_SERVER_AUTH_SESSION_SECRET 環境変数から設定される。
        /// </summary>
        public static byte[] AuthSecretKey { get; private set; }

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
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // --- コマンドライン引数解析 ---
            int playerCount = ParsePlayerCount();
            ushort port = ParsePort();
            int healthPort = ParseHealthPort();
            string address = ParseAddress();
            string matchId = ParseMatchId();
            byte[] secretKey = ParseSecret();

            Debug.Log($"[ServerBootstrap] Starting Fusion Server, port={port}, health={healthPort}, players={playerCount}, address={address ?? "(auto)"}, matchId={matchId ?? "(none)"}...");

            // --- TCP ヘルスプローブ開始 ---
            _healthProbe = new TcpHealthProbe(healthPort);
            _healthProbe.Start();
            Application.quitting += () =>
            {
                _healthProbe?.Dispose();
                _healthProbe = null;
            };

            // Dedicated Server の接続情報を一括設定
            SurvivorNetworkMatchConnector.ConfigureForDedicatedServer(port, address, matchId);
            SurvivorNetworkMatchConnector.SetExpectedPlayerCount(playerCount);

            if (secretKey != null)
            {
                AuthSecretKey = secretKey;
                Debug.Log("[ServerBootstrap] AuthSecretKey が設定されました（HMAC 認証有効）");
            }

            // Fusion Server セッションは SurvivorFusionStageConnector.StartServerAsync() で開始される
        }

        private static ushort ParsePort()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--port" && ushort.TryParse(args[i + 1], out ushort port))
                    return port;
            }
            return 7777;
        }

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

        private static string ParseAddress()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--address" && !string.IsNullOrEmpty(args[i + 1]))
                    return args[i + 1];
            }
            return null;
        }

        private static int ParseHealthPort()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--health-port" && int.TryParse(args[i + 1], out int port))
                    return port;
            }
            return 7778;
        }

        /// <summary>
        /// --match-id 引数からマッチ ID を取得する。
        /// </summary>
        private static string ParseMatchId()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--match-id")
                    return args[i + 1];
            }
            return null;
        }

        /// <summary>
        /// --secret 引数または UNITY_SERVER_AUTH_SESSION_SECRET 環境変数から HMAC シークレットを取得する。
        /// </summary>
        private static byte[] ParseSecret()
        {
            // コマンドライン引数を優先
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--secret" && !string.IsNullOrEmpty(args[i + 1]))
                    return Encoding.UTF8.GetBytes(args[i + 1]);
            }

            // 環境変数からも取得可能
            var envSecret = System.Environment.GetEnvironmentVariable("UNITY_SERVER_AUTH_SESSION_SECRET");
            if (!string.IsNullOrEmpty(envSecret))
                return Encoding.UTF8.GetBytes(envSecret);

            return null;
        }
    }
}
