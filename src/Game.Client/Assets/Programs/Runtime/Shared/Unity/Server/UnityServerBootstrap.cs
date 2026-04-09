using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using Game.Shared.Network.Survivor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.Shared.Unity.Server
{
    /// <summary>
    /// Dedicated Server 起動時の初期化処理。
    /// Fusion Server モードで起動し、クライアント接続を受け入れる。
    /// GameRuntimeInitializer からサーバーモード時に明示的に呼び出される。
    /// </summary>
    public static class UnityServerBootstrap
    {
        /// <summary>
        /// HMAC 認証用シークレットキー。
        /// --secret 引数または UNITY_SERVER_AUTH_SESSION_SECRET 環境変数から設定される。
        /// </summary>
        public static byte[] AuthSecretKey { get; private set; }

        /// <summary>
        /// この DS の一意識別子（起動時に生成）。
        /// </summary>
        public static string DsId { get; private set; }

        /// <summary>
        /// Game.Server の URL（自己登録・ハートビートに使用）。
        /// </summary>
        public static string GameServerUrl { get; private set; }

        /// <summary>
        /// ServerHttpListener インスタンス（SurvivorServerGameLoop からアクセス可能）。
        /// </summary>
        public static ServerHttpListener HttpListener { get; private set; }

        private static Thread _heartbeatThread;
        private static volatile bool _heartbeatRunning;
        private static ushort _gamePort;
        private static int _healthPort;
        private static string _address;

        /// <summary>
        /// Dedicated Server の初期化処理を実行する。
        /// メインスレッドから呼ぶこと。
        /// </summary>
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

            // --- DS 識別子生成 ---
            DsId = $"ds-{Guid.NewGuid():N}";
            Debug.Log($"[ServerBootstrap] DsId: {DsId}");

            // --- コマンドライン引数解析 ---
            int playerCount = ParsePlayerCount();
            _gamePort = ParsePort();
            _healthPort = ParseHealthPort();
            _address = ParseAddress();
            string matchId = ParseMatchId();
            byte[] secretKey = ParseSecret();
            GameServerUrl = ParseGameServerUrl();

            Debug.Log($"[ServerBootstrap] port={_gamePort}, health={_healthPort}, players={playerCount}, address={_address ?? "(auto)"}, matchId={matchId ?? "(none)"}");
            Debug.Log($"[ServerBootstrap] GameServerUrl={GameServerUrl ?? "(none)"}");

            // --- ServerHttpListener 起動 ---
            HttpListener = new ServerHttpListener(_healthPort, DsId);
            if (secretKey != null)
            {
                HttpListener.SetAuthSecretKey(secretKey);
            }
            HttpListener.Start();

            // --- シークレットキー保持 ---
            if (secretKey != null)
            {
                AuthSecretKey = secretKey;
                Debug.Log("[ServerBootstrap] AuthSecretKey が設定されました（HMAC 認証有効）");
            }

            // --- Dedicated Server 接続情報設定（セッション開始前のデフォルト）---
            // セッションリクエスト受信後に SurvivorServerGameLoop が上書きする
            SurvivorNetworkMatchConnector.ConfigureForDedicatedServer(_gamePort, _address, matchId);
            SurvivorNetworkMatchConnector.SetExpectedPlayerCount(playerCount);

            // --- Application.quitting ハンドラー登録 ---
            Application.quitting += OnApplicationQuitting;

            // --- Game.Server への自己登録（バックグラウンドスレッドで実行）---
            if (!string.IsNullOrEmpty(GameServerUrl))
            {
                RegisterToGameServer();
                StartHeartbeat();
            }
            else
            {
                Debug.LogWarning("[ServerBootstrap] GAME_SERVER_URL が未設定のため自己登録をスキップします");
            }
        }

        // ---------------------------------------------------------------
        // 自己登録・ハートビート・登録解除
        // ---------------------------------------------------------------

        /// <summary>
        /// Game.Server に DS を自己登録する（バックグラウンドスレッドで実行）。
        /// </summary>
        private static void RegisterToGameServer()
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var dsAddress = string.IsNullOrEmpty(_address) ? GetLocalAddress() : _address;
                    var body = BuildRegistrationJson(dsAddress);
                    var response = SendHttpPost(
                        $"{GameServerUrl}/api/unity-server/register",
                        body,
                        AuthSecretKey);

                    Debug.Log($"[ServerBootstrap] 自己登録完了: status={response}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ServerBootstrap] 自己登録失敗: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 30 秒間隔のハートビートを開始する（バックグラウンドスレッド）。
        /// </summary>
        private static void StartHeartbeat()
        {
            _heartbeatRunning = true;
            _heartbeatThread = new Thread(HeartbeatLoop)
            {
                Name = "DS-Heartbeat",
                IsBackground = true,
            };
            _heartbeatThread.Start();
            Debug.Log("[ServerBootstrap] ハートビート開始（30秒間隔）");
        }

        private static void HeartbeatLoop()
        {
            while (_heartbeatRunning)
            {
                // 30 秒待機（1 秒ずつチェックして停止フラグを確認）
                for (int i = 0; i < 30 && _heartbeatRunning; i++)
                    Thread.Sleep(1000);

                if (!_heartbeatRunning)
                    break;

                try
                {
                    var url = $"{GameServerUrl}/api/unity-server/heartbeat?dsId={Uri.EscapeDataString(DsId)}";
                    var response = SendHttpPost(url, "{}", AuthSecretKey);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[ServerBootstrap] ハートビート送信: status={response}");
#endif
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ServerBootstrap] ハートビート失敗: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Game.Server に登録解除を通知し、リソースを解放する。
        /// Application.quitting で呼ばれる。
        /// </summary>
        private static void OnApplicationQuitting()
        {
            Debug.Log("[ServerBootstrap] Application quitting, クリーンアップ開始");

            // ハートビート停止
            _heartbeatRunning = false;
            if (_heartbeatThread != null && _heartbeatThread.IsAlive)
                _heartbeatThread.Join(2000);

            // ServerHttpListener 停止
            try
            {
                HttpListener?.Dispose();
                HttpListener = null;
            }
            catch (Exception) { }

            // Game.Server へ登録解除通知
            if (!string.IsNullOrEmpty(GameServerUrl))
            {
                try
                {
                    var url = $"{GameServerUrl}/api/unity-server/deregister?dsId={Uri.EscapeDataString(DsId)}";
                    SendHttpPost(url, "{}", AuthSecretKey);
                    Debug.Log("[ServerBootstrap] 登録解除完了");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ServerBootstrap] 登録解除失敗: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// セッション終了を Game.Server に通知する。
        /// SurvivorServerGameLoop から呼ぶ。
        /// </summary>
        /// <param name="matchId">終了したセッションのマッチID。</param>
        public static void NotifySessionEnded(string matchId)
        {
            if (string.IsNullOrEmpty(GameServerUrl))
                return;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var url = $"{GameServerUrl}/api/unity-server/session-ended"
                              + $"?dsId={Uri.EscapeDataString(DsId)}"
                              + $"&matchId={Uri.EscapeDataString(matchId ?? string.Empty)}";
                    SendHttpPost(url, "{}", AuthSecretKey);
                    Debug.Log($"[ServerBootstrap] セッション終了通知送信: matchId={matchId}");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ServerBootstrap] セッション終了通知失敗: {ex.Message}");
                }
            });
        }

        // ---------------------------------------------------------------
        // HTTP ユーティリティ（System.Net.Http.HttpClient 使用、IL2CPP 互換）
        // ---------------------------------------------------------------

        /// <summary>
        /// HTTP POST を同期実行する。バックグラウンドスレッドから呼ぶこと。
        /// </summary>
        /// <param name="url">送信先 URL。</param>
        /// <param name="jsonBody">JSON ボディ文字列。</param>
        /// <param name="secretKey">認証シークレット（null で認証ヘッダーなし）。</param>
        /// <returns>HTTP ステータスコード文字列。</returns>
        private static string SendHttpPost(string url, string jsonBody, byte[] secretKey)
        {
            using var handler = new HttpClientHandler();
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };

            if (secretKey != null && secretKey.Length > 0)
                client.DefaultRequestHeaders.Add("X-DS-Auth", Encoding.UTF8.GetString(secretKey));

            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            var response = client.PostAsync(url, content).Result;
            return $"{(int)response.StatusCode} {response.StatusCode}";
        }

        // ---------------------------------------------------------------
        // ヘルパーメソッド
        // ---------------------------------------------------------------

        private static string BuildRegistrationJson(string dsAddress)
        {
            return $"{{\"dsId\":\"{EscapeJson(DsId)}\","
                   + $"\"address\":\"{EscapeJson(dsAddress)}\","
                   + $"\"gamePort\":{_gamePort},"
                   + $"\"healthPort\":{_healthPort}}}";
        }

        private static string GetLocalAddress()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        return ip.ToString();
                }
            }
            catch { }

            return "127.0.0.1";
        }

        private static string EscapeJson(string value)
        {
            if (value == null)
                return string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

        // ---------------------------------------------------------------
        // コマンドライン引数パース
        // ---------------------------------------------------------------

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
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--secret" && !string.IsNullOrEmpty(args[i + 1]))
                    return Encoding.UTF8.GetBytes(args[i + 1]);
            }

            var envSecret = System.Environment.GetEnvironmentVariable("UNITY_SERVER_AUTH_SESSION_SECRET");
            if (!string.IsNullOrEmpty(envSecret))
                return Encoding.UTF8.GetBytes(envSecret);

            return null;
        }

        /// <summary>
        /// --game-server-url 引数または GAME_SERVER_URL 環境変数から Game.Server URL を取得する。
        /// </summary>
        private static string ParseGameServerUrl()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--game-server-url" && !string.IsNullOrEmpty(args[i + 1]))
                    return args[i + 1].TrimEnd('/');
            }

            var envUrl = System.Environment.GetEnvironmentVariable("GAME_SERVER_URL");
            if (!string.IsNullOrEmpty(envUrl))
                return envUrl.TrimEnd('/');

            return "http://localhost:5000";
        }
    }
}
