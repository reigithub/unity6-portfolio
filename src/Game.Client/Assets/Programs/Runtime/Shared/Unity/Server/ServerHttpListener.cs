using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Shared.Unity.Server
{
    /// <summary>
    /// Dedicated Server 向け軽量 HTTP サーバー。
    /// TcpListener + 手動 HTTP パースで実装（IL2CPP 安全性のため HttpListener は使用しない）。
    /// バックグラウンドスレッドでリクエストを受け付け、ConcurrentQueue でメインスレッドとブリッジする。
    /// </summary>
    /// <remarks>
    /// エンドポイント:
    /// - GET  /health           → DS ステータス JSON
    /// - POST /session/start    → セッション作成リクエスト（ConcurrentQueue 経由でメインスレッドへ）
    /// - GET  /sessions         → 現在のセッション状態
    /// </remarks>
    public sealed class ServerHttpListener : IDisposable
    {
        // ---------------------------------------------------------------
        // セッション作成リクエストの内部クラス
        // ---------------------------------------------------------------

        /// <summary>
        /// バックグラウンドスレッドからメインスレッドへ渡すセッション作成リクエスト。
        /// </summary>
        public class SessionStartRequest
        {
            /// <summary>マッチID（Fusion セッション識別子）。</summary>
            public string MatchId;

            /// <summary>ステージID。</summary>
            public int StageId;

            /// <summary>期待プレイヤー数。</summary>
            public int ExpectedPlayers;

            /// <summary>
            /// メインスレッドが処理完了後に SetResult を呼ぶことで HTTP レスポンスを返す。
            /// </summary>
            public TaskCompletionSource<bool> CompletionSource = new TaskCompletionSource<bool>();
        }

        // ---------------------------------------------------------------
        // フィールド
        // ---------------------------------------------------------------

        private readonly int _port;
        private readonly string _dsId;
        private readonly DateTime _startTime;
        private TcpListener _listener;
        private Thread _thread;
        private volatile bool _running;

        // バックグラウンド → メインスレッド ブリッジ
        private readonly ConcurrentQueue<SessionStartRequest> _pendingRequests
            = new ConcurrentQueue<SessionStartRequest>();

        // 現在のセッション状態
        private volatile string _currentMatchId;
        private volatile string _currentStatus = "idle";

        // DS 認証シークレット（null = 認証スキップ）
        private byte[] _authSecretKey;

        // ---------------------------------------------------------------
        // プロパティ
        // ---------------------------------------------------------------

        /// <summary>現在の DS ステータス。"idle" または "active"。</summary>
        public string Status => _currentStatus;

        /// <summary>現在実行中のマッチID。idle 時は null。</summary>
        public string CurrentMatchId => _currentMatchId;

        /// <summary>起動からの経過秒数。</summary>
        public long UptimeSeconds => (long)(DateTime.UtcNow - _startTime).TotalSeconds;

        // ---------------------------------------------------------------
        // コンストラクタ
        // ---------------------------------------------------------------

        /// <summary>
        /// ServerHttpListener を初期化する。
        /// </summary>
        /// <param name="port">HTTP リスンポート番号。</param>
        /// <param name="dsId">この DS の一意識別子。</param>
        public ServerHttpListener(int port, string dsId)
        {
            _port = port;
            _dsId = dsId;
            _startTime = DateTime.UtcNow;
        }

        // ---------------------------------------------------------------
        // 公開メソッド
        // ---------------------------------------------------------------

        /// <summary>
        /// DS 認証シークレットを設定する。
        /// 未設定（null）の場合は X-DS-Auth ヘッダー検証をスキップする（ローカル開発用）。
        /// </summary>
        /// <param name="secretKey">HMAC シークレットのバイト配列。</param>
        public void SetAuthSecretKey(byte[] secretKey)
        {
            _authSecretKey = secretKey;
        }

        /// <summary>
        /// HTTP リスナーをバックグラウンドスレッドで起動する。
        /// </summary>
        public void Start()
        {
            if (_running)
                return;

            _running = true;
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();

            _thread = new Thread(ListenLoop)
            {
                Name = "ServerHttpListener",
                IsBackground = true,
            };
            _thread.Start();

            Debug.Log($"[ServerHttpListener] Listening on HTTP port {_port}, dsId={_dsId}");
        }

        /// <summary>
        /// メインスレッドからセッション作成リクエストをデキューする。
        /// SurvivorServerGameLoop から毎フレームまたは一定間隔で呼ぶ。
        /// </summary>
        /// <param name="request">デキューしたリクエスト。</param>
        /// <returns>リクエストが存在した場合は true。</returns>
        public bool TryDequeueSessionRequest(out SessionStartRequest request)
        {
            return _pendingRequests.TryDequeue(out request);
        }

        /// <summary>
        /// セッション状態を active に更新する。
        /// メインスレッドから呼ぶ。
        /// </summary>
        /// <param name="matchId">開始したセッションのマッチID。</param>
        public void SetSessionActive(string matchId)
        {
            _currentMatchId = matchId;
            _currentStatus = "active";
            Debug.Log($"[ServerHttpListener] Session active: matchId={matchId}");
        }

        /// <summary>
        /// セッション状態を idle に戻す。
        /// メインスレッドから呼ぶ。
        /// </summary>
        public void SetSessionIdle()
        {
            _currentMatchId = null;
            _currentStatus = "idle";
            Debug.Log("[ServerHttpListener] Session idle (waiting for next session)");
        }

        /// <summary>
        /// HTTP リスナーを停止してリソースを解放する。
        /// </summary>
        public void Dispose()
        {
            _running = false;

            try
            {
                _listener?.Stop();
            }
            catch (Exception)
            {
                // 停止時の例外は無視
            }

            if (_thread != null && _thread.IsAlive)
                _thread.Join(2000);

            _listener = null;
            _thread = null;

            Debug.Log("[ServerHttpListener] Stopped");
        }

        // ---------------------------------------------------------------
        // バックグラウンドスレッド処理
        // ---------------------------------------------------------------

        private void ListenLoop()
        {
            while (_running)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    // 接続ごとに別スレッドで処理（TcpListener を長時間ブロックしない）
                    ThreadPool.QueueUserWorkItem(_ => HandleClient(client));
                }
                catch (SocketException)
                {
                    if (!_running)
                        break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    var (method, path, headers, body) = ParseHttpRequest(stream);
                    if (method == null)
                        return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[ServerHttpListener] {method} {path}");
#endif

                    // DS 認証チェック（シークレット未設定時はスキップ）
                    if (_authSecretKey != null && _authSecretKey.Length > 0)
                    {
                        if (!ValidateAuth(headers))
                        {
                            WriteResponse(stream, 401, "{\"error\":\"Unauthorized\"}");
                            return;
                        }
                    }

                    // ルーティング
                    if (method == "GET" && path == "/health")
                    {
                        HandleHealth(stream);
                    }
                    else if (method == "POST" && path == "/session/start")
                    {
                        HandleSessionStart(stream, body);
                    }
                    else if (method == "GET" && path == "/sessions")
                    {
                        HandleSessions(stream);
                    }
                    else
                    {
                        WriteResponse(stream, 404, "{\"error\":\"Not Found\"}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ServerHttpListener] クライアント処理中にエラー: {ex.Message}");
            }
        }

        // ---------------------------------------------------------------
        // エンドポイントハンドラー
        // ---------------------------------------------------------------

        private void HandleHealth(NetworkStream stream)
        {
            var matchIdJson = _currentMatchId == null ? "null" : $"\"{EscapeJson(_currentMatchId)}\"";
            var json = $"{{\"dsId\":\"{EscapeJson(_dsId)}\","
                       + $"\"status\":\"{EscapeJson(_currentStatus)}\","
                       + $"\"currentMatchId\":{matchIdJson},"
                       + $"\"uptimeSeconds\":{UptimeSeconds}}}";
            WriteResponse(stream, 200, json);
        }

        private void HandleSessions(NetworkStream stream)
        {
            // 現在は 1 DS = 1 セッションのため health と同等の情報を返す
            HandleHealth(stream);
        }

        private void HandleSessionStart(NetworkStream stream, string body)
        {
            // JSON を手動パース（JsonUtility は static フィールドなし DTO に対応しにくいため）
            if (!TryParseSessionStartBody(body, out var matchId, out var stageId, out var expectedPlayers))
            {
                WriteResponse(stream, 400, "{\"error\":\"Invalid request body\"}");
                return;
            }

            // 既にアクティブなセッションがある場合は拒否
            if (_currentStatus == "active")
            {
                WriteResponse(stream, 409, "{\"error\":\"Session already active\"}");
                return;
            }

            // ConcurrentQueue にエンキュー → メインスレッドで処理
            var request = new SessionStartRequest
            {
                MatchId = matchId,
                StageId = stageId,
                ExpectedPlayers = expectedPlayers,
                CompletionSource = new TaskCompletionSource<bool>(),
            };
            _pendingRequests.Enqueue(request);

            Debug.Log($"[ServerHttpListener] Session start request enqueued: matchId={matchId}, stageId={stageId}, players={expectedPlayers}");

            // メインスレッドの処理完了を待機（最大 30 秒、Fusion の Photon Cloud 接続に数秒かかる）
            bool completed = request.CompletionSource.Task.Wait(TimeSpan.FromSeconds(30));
            if (!completed)
            {
                WriteResponse(stream, 504, "{\"error\":\"Session start timeout\"}");
                return;
            }

            bool success = request.CompletionSource.Task.Result;
            if (success)
            {
                var responseJson = $"{{\"matchId\":\"{EscapeJson(matchId)}\","
                                   + $"\"sessionName\":\"{EscapeJson(matchId)}\","
                                   + $"\"success\":true,"
                                   + $"\"errorMessage\":\"\"}}";
                WriteResponse(stream, 200, responseJson);
            }
            else
            {
                WriteResponse(stream, 500, "{\"error\":\"Session start failed\"}");
            }
        }

        // ---------------------------------------------------------------
        // HTTP パース・レスポンスユーティリティ
        // ---------------------------------------------------------------

        private static (string method, string path, System.Collections.Generic.Dictionary<string, string> headers, string body)
            ParseHttpRequest(NetworkStream stream)
        {
            var headers = new System.Collections.Generic.Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

            try
            {
                // StreamReader は NetworkStream のオーナーシップを取らない（leaveOpen: true）
                var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false,
                    bufferSize: 4096, leaveOpen: true);

                // リクエストライン
                var requestLine = reader.ReadLine();
                if (string.IsNullOrEmpty(requestLine))
                    return (null, null, headers, null);

                var parts = requestLine.Split(' ');
                if (parts.Length < 2)
                    return (null, null, headers, null);

                var method = parts[0].ToUpperInvariant();
                var path = parts[1];

                // ヘッダー読み込み
                int contentLength = 0;
                string line;
                while (!string.IsNullOrEmpty(line = reader.ReadLine()))
                {
                    int colon = line.IndexOf(':');
                    if (colon > 0)
                    {
                        var key = line.Substring(0, colon).Trim();
                        var value = line.Substring(colon + 1).Trim();
                        headers[key] = value;

                        if (string.Equals(key, "Content-Length", StringComparison.OrdinalIgnoreCase)
                            && int.TryParse(value, out var len))
                        {
                            contentLength = len;
                        }
                    }
                }

                // ボディ読み込み
                string body = null;
                if (contentLength > 0)
                {
                    var buffer = new char[contentLength];
                    int read = reader.Read(buffer, 0, contentLength);
                    body = new string(buffer, 0, read);
                }

                return (method, path, headers, body);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ServerHttpListener] HTTP パース失敗: {ex.Message}");
                return (null, null, headers, null);
            }
        }

        private static void WriteResponse(NetworkStream stream, int statusCode, string jsonBody)
        {
            try
            {
                var statusText = statusCode switch
                {
                    200 => "OK",
                    400 => "Bad Request",
                    401 => "Unauthorized",
                    404 => "Not Found",
                    409 => "Conflict",
                    500 => "Internal Server Error",
                    504 => "Gateway Timeout",
                    _ => "Unknown",
                };

                var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
                var response = $"HTTP/1.1 {statusCode} {statusText}\r\n"
                               + "Content-Type: application/json\r\n"
                               + $"Content-Length: {bodyBytes.Length}\r\n"
                               + "Connection: close\r\n"
                               + "\r\n";

                var headerBytes = Encoding.UTF8.GetBytes(response);
                stream.Write(headerBytes, 0, headerBytes.Length);
                stream.Write(bodyBytes, 0, bodyBytes.Length);
                stream.Flush();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ServerHttpListener] レスポンス書き込み失敗: {ex.Message}");
            }
        }

        // ---------------------------------------------------------------
        // 認証・JSON パースユーティリティ
        // ---------------------------------------------------------------

        private bool ValidateAuth(System.Collections.Generic.Dictionary<string, string> headers)
        {
            if (!headers.TryGetValue("X-DS-Auth", out var authHeader))
                return false;

            // バイト列比較（タイミング攻撃対策は省略。内部ネットワーク前提）
            var expected = Encoding.UTF8.GetString(_authSecretKey);
            return authHeader == expected;
        }

        /// <summary>
        /// POST /session/start のボディを手動パースする。
        /// 期待フォーマット: {"matchId":"...","stageId":1,"expectedPlayers":2}
        /// </summary>
        private static bool TryParseSessionStartBody(string body, out string matchId, out int stageId, out int expectedPlayers)
        {
            matchId = null;
            stageId = 0;
            expectedPlayers = 0;

            if (string.IsNullOrEmpty(body))
                return false;

            try
            {
                matchId = ExtractJsonString(body, "matchId");
                stageId = ExtractJsonInt(body, "stageId");
                expectedPlayers = ExtractJsonInt(body, "expectedPlayers");
                return !string.IsNullOrEmpty(matchId);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>JSON 文字列から指定キーの文字列値を取り出す（簡易実装）。</summary>
        private static string ExtractJsonString(string json, string key)
        {
            var searchKey = $"\"{key}\"";
            int keyIdx = json.IndexOf(searchKey, StringComparison.Ordinal);
            if (keyIdx < 0)
                return null;

            int colonIdx = json.IndexOf(':', keyIdx + searchKey.Length);
            if (colonIdx < 0)
                return null;

            int quoteStart = json.IndexOf('"', colonIdx + 1);
            if (quoteStart < 0)
                return null;

            int quoteEnd = json.IndexOf('"', quoteStart + 1);
            if (quoteEnd < 0)
                return null;

            return json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
        }

        /// <summary>JSON 文字列から指定キーの整数値を取り出す（簡易実装）。</summary>
        private static int ExtractJsonInt(string json, string key)
        {
            var searchKey = $"\"{key}\"";
            int keyIdx = json.IndexOf(searchKey, StringComparison.Ordinal);
            if (keyIdx < 0)
                return 0;

            int colonIdx = json.IndexOf(':', keyIdx + searchKey.Length);
            if (colonIdx < 0)
                return 0;

            // 数字の開始位置を探す
            int numStart = colonIdx + 1;
            while (numStart < json.Length && (json[numStart] == ' ' || json[numStart] == '\t'))
                numStart++;

            int numEnd = numStart;
            while (numEnd < json.Length && (char.IsDigit(json[numEnd]) || json[numEnd] == '-'))
                numEnd++;

            if (numEnd == numStart)
                return 0;

            int.TryParse(json.Substring(numStart, numEnd - numStart), out int result);
            return result;
        }

        /// <summary>JSON 文字列内の特殊文字をエスケープする。</summary>
        private static string EscapeJson(string value)
        {
            if (value == null)
                return string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }
}
