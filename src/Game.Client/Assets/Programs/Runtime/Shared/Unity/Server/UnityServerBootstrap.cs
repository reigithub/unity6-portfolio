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
            int healthPort = ParseHealthPort();

            Debug.Log($"[ServerBootstrap] Starting Fusion Server, health={healthPort}, players={playerCount}...");

            // --- TCP ヘルスプローブ開始 ---
            _healthProbe = new TcpHealthProbe(healthPort);
            _healthProbe.Start();
            Application.quitting += () =>
            {
                _healthProbe?.Dispose();
                _healthProbe = null;
            };

            // プレイヤー数を保存（SurvivorFusionServerSession が後で使用）
            SurvivorNetworkMatchConnector.SetExpectedPlayerCount(playerCount);

            // Fusion Server セッションは SurvivorFusionStageConnector.StartServerAsync() で開始される
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
    }
}
