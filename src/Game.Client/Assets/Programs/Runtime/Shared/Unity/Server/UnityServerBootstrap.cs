using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using Game.Shared.Environment;
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

        /// <summary>
        /// Dedicated Server の初期化処理を実行する。
        /// メインスレッドから呼ぶこと。
        /// </summary>
        private static bool _initialized;

        /// <summary>
        /// Dedicated Server の初期化処理を実行する。
        /// </summary>
        /// <param name="sessionConnector">接続パラメータ設定に使用する <see cref="ISurvivorNetworkSessionConnector"/> インスタンス。</param>
        public static void Initialize(ISurvivorNetworkSessionConnector sessionConnector)
        {
            if (_initialized) return;
            _initialized = true;

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
            var args = ClArgsHelper.Parse();
            _gamePort = ParsePort(args);
            _healthPort = ParseHealthPort(args);
            byte[] secretKey = ParseSecret();
            GameServerUrl = ParseGameServerUrl();

            Debug.Log($"[ServerBootstrap] port={_gamePort}, health={_healthPort}");
            Debug.Log($"[ServerBootstrap] GameServerUrl={GameServerUrl ?? "(none)"}");

            // --- GCE 環境なら外部 IP を自動取得して PUBLIC_ADDRESS 環境変数に設定 ---
            // 非 GCE 環境では 2 秒 timeout で silent fail する
            EnvVarHelper.Set(EnvVarKeys.PublicAddress, () => TryFetchGceExternalIp());

            // --- ServerHttpListener 起動 ---
            HttpListener = new ServerHttpListener(_healthPort, DsId);
            HttpListener.SetAuthSecretKey(secretKey);
            HttpListener.Start();

            // --- シークレットキー保持 ---
            if (secretKey != null)
            {
                AuthSecretKey = secretKey;
                Debug.Log("[ServerBootstrap] AuthSecretKey が設定されました（HMAC 認証有効）");
            }

            // --- Dedicated Server 接続情報設定（Fusion バインドポートのみ）---
            // SessionName / MaxPlayerCount はセッションリクエスト受信時に
            // SurvivorServerGameLoop が UpdateConfigure で設定する。
            sessionConnector.Configure(ConnectionSource.DedicatedServer, port: _gamePort);

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
                    var dsAddress = GetLocalAddress();
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
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return ip.ToString();
            }

            return null;
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

        /// <summary>
        /// Fusion UDP ポートを解決する。優先順位: CLI 引数 → 環境変数 UNITY_SERVER_PORT → デフォルト 7777。
        /// </summary>
        private static ushort ParsePort(Dictionary<string, string> args)
        {
            // var args = System.Environment.GetCommandLineArgs();
            // for (int i = 0; i < args.Length - 1; i++)
            // {
            //     if (args[i] == "--port" && ushort.TryParse(args[i + 1], out ushort cliPort))
            //         return cliPort;
            // }

            if (ClArgsHelper.TryGet(args, "--port", out ushort cliPort ,p => ushort.Parse(p)))
                return cliPort;

            // var envPort = System.Environment.GetEnvironmentVariable(EnvVarKeys.UnityServerPort);
            // if (!string.IsNullOrEmpty(envPort) && ushort.TryParse(envPort, out ushort parsedEnvPort))
            //     return parsedEnvPort;

            if (EnvVarHelper.TryGet(EnvVarKeys.UnityServerPort, out ushort port, p => ushort.Parse(p)))
                return port;

            return 7777;
        }

        /// <summary>
        /// ヘルスチェック TCP ポートを解決する。優先順位: CLI 引数 → 環境変数 UNITY_SERVER_HEALTH_PORT → デフォルト 7778。
        /// </summary>
        private static int ParseHealthPort(Dictionary<string, string> args)
        {
            // var args = System.Environment.GetCommandLineArgs();
            // for (int i = 0; i < args.Length - 1; i++)
            // {
            //     if (args[i] == "--health-port" && int.TryParse(args[i + 1], out int cliPort))
            //         return cliPort;
            // }

            if (ClArgsHelper.TryGet(args, "--health-port", out int cliPort ,p => int.Parse(p)))
                return cliPort;

            if (EnvVarHelper.TryGet(EnvVarKeys.UnityServerHealthPort, out int port, p => int.Parse(p)))
                return port;

            // var envPort = System.Environment.GetEnvironmentVariable(EnvVarKeys.UnityServerHealthPort);
            // if (!string.IsNullOrEmpty(envPort) && int.TryParse(envPort, out int parsedEnvPort))
            //     return parsedEnvPort;

            return 7778;
        }

        /// <summary>
        /// 環境変数 UNITY_SERVER_AUTH_SESSION_SECRET から HMAC シークレットを取得する。
        /// </summary>
        private static byte[] ParseSecret()
        {
            EnvVarHelper.TryGet(EnvVarKeys.UnityServerAuthSecretKey, out byte[] secret, s => Encoding.UTF8.GetBytes(s));
            return secret;

            // var envSecret = System.Environment.GetEnvironmentVariable(EnvVarKeys.UnityServerAuthSecretKey);
            // return !string.IsNullOrEmpty(envSecret) ? Encoding.UTF8.GetBytes(envSecret) : null;
        }

        /// <summary>
        /// 環境変数 GAME_SERVER_URL から Game.Server URL を取得する。
        /// 未設定時は null を返し、Initialize 内のガードで自己登録をスキップする。
        /// </summary>
        private static string ParseGameServerUrl()
        {
            EnvVarHelper.TryGet(EnvVarKeys.GameServerUrl, out string url, u => u.TrimEnd('/'));
            return url;

            // var envUrl = System.Environment.GetEnvironmentVariable(EnvVarKeys.GameServerUrl);
            // return !string.IsNullOrEmpty(envUrl) ? envUrl.TrimEnd('/') : null;
        }

        /// <summary>
        /// GCE metadata server から外部 IP を取得する。
        /// 非 GCE 環境 (Editor / ローカル Docker / オンプレ) では名前解決失敗 or 2 秒 timeout で null を返す。
        /// </summary>
        private static string TryFetchGceExternalIp()
        {
            try
            {
                using var handler = new HttpClientHandler();
                using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2) };
                client.DefaultRequestHeaders.Add("Metadata-Flavor", "Google");
                var url = "http://metadata.google.internal/computeMetadata/v1/instance/network-interfaces/0/access-configs/0/external-ip";
                return client.GetStringAsync(url).Result?.Trim();
            }
            catch
            {
                // 非 GCE 環境では timeout / 名前解決失敗で null を返す
                return null;
            }
        }
    }
}
