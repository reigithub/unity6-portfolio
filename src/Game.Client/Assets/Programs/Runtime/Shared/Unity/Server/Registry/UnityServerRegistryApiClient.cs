using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Game.Library.Shared.Dto;
using Game.Library.Shared.RequestSigning;
using MessagePack;
using UnityEngine;
using VContainer;

namespace Game.Shared.Unity.Server
{
    /// <summary>
    /// <see cref="IUnityServerRegistryApiClient"/> の実装。
    /// Game.Server の <c>/api/unity-server/*</c> エンドポイントに対して HTTP 通信を行う。
    /// <c>static readonly HttpClient</c> でプロセス共有し、ソケット枯渇を防ぐ。
    /// 各リクエストには HMAC-SHA256 署名（X-Signature / X-Timestamp / X-Nonce）を付与する。
    /// </summary>
    public sealed class UnityServerRegistryApiClient : IUnityServerRegistryApiClient
    {
        // プロセス共有 HttpClient（ソケット枯渇防止）
        private static readonly HttpClient s_httpClient = new HttpClient(new HttpClientHandler())
        {
            Timeout = TimeSpan.FromSeconds(10),
        };

        private readonly UnityServerConfigProvider _configProvider;

        /// <summary>
        /// <see cref="UnityServerRegistryApiClient"/> を初期化する。
        /// </summary>
        /// <param name="configProvider">設定プロバイダ。</param>
        [Inject]
        public UnityServerRegistryApiClient(UnityServerConfigProvider configProvider)
        {
            _configProvider = configProvider;
        }

        /// <inheritdoc/>
        public async Task<bool> RegisterAsync(string dsAddress, string internalAddress, CancellationToken ct)
        {
            var config = _configProvider.Current;
            if (string.IsNullOrEmpty(config.GameServerUrl))
                return true;

            try
            {
                var request = new UnityServerRegistrationRequest
                {
                    DsId = config.DsId,
                    Address = dsAddress,
                    InternalAddress = internalAddress,
                    GamePort = config.GamePort,
                    HealthPort = config.HealthPort,
                };
                var url = $"{config.GameServerUrl}/api/unity-server/register";
                var status = await PostMessagePackAsync(url, request, config.AuthSecretKey, ct);
                Debug.Log($"[UnityServerRegistryApiClient] 自己登録完了: status={status}, internalAddress={internalAddress ?? "(none)"}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityServerRegistryApiClient] 自己登録失敗: {ex.Message}");
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> HeartbeatAsync(CancellationToken ct)
        {
            var config = _configProvider.Current;
            if (string.IsNullOrEmpty(config.GameServerUrl))
                return true;

            try
            {
                var request = new UnityServerHeartbeatRequest { DsId = config.DsId };
                var url = $"{config.GameServerUrl}/api/unity-server/heartbeat";
                var status = await PostMessagePackAsync(url, request, config.AuthSecretKey, ct);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[UnityServerRegistryApiClient] ハートビート送信: status={status}");
#endif
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnityServerRegistryApiClient] ハートビート失敗: {ex.Message}");
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeregisterAsync(CancellationToken ct)
        {
            var config = _configProvider.Current;
            if (string.IsNullOrEmpty(config.GameServerUrl))
                return true;

            try
            {
                var request = new UnityServerDeregisterRequest { DsId = config.DsId };
                var url = $"{config.GameServerUrl}/api/unity-server/deregister";
                var status = await PostMessagePackAsync(url, request, config.AuthSecretKey, ct);
                Debug.Log($"[UnityServerRegistryApiClient] 登録解除完了: status={status}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnityServerRegistryApiClient] 登録解除失敗: {ex.Message}");
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> NotifySessionEndedAsync(string matchId, CancellationToken ct)
        {
            var config = _configProvider.Current;
            if (string.IsNullOrEmpty(config.GameServerUrl))
                return true;

            try
            {
                var request = new UnityServerSessionEndedRequest
                {
                    DsId = config.DsId,
                    MatchId = matchId ?? string.Empty,
                };
                var url = $"{config.GameServerUrl}/api/unity-server/session-ended";
                var status = await PostMessagePackAsync(url, request, config.AuthSecretKey, ct);
                Debug.Log($"[UnityServerRegistryApiClient] セッション終了通知送信: matchId={matchId}, status={status}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnityServerRegistryApiClient] セッション終了通知失敗: {ex.Message}");
                return false;
            }
        }

        // ---------------------------------------------------------------
        // HTTP ユーティリティ
        // ---------------------------------------------------------------

        /// <summary>
        /// MessagePack 形式で body を送信し HMAC 署名ヘッダを付与する HTTP POST ヘルパー。
        /// <c>application/x-msgpack</c> Content-Type で送り、
        /// サーバー側の <c>MessagePackInputFormatter</c> により DTO に自動バインドされる。
        /// </summary>
        /// <param name="url">送信先 URL。</param>
        /// <param name="body">リクエストボディ DTO。</param>
        /// <param name="secret">HMAC 署名用シークレット。空の場合は署名ヘッダを付与しない。</param>
        /// <param name="ct">キャンセルトークン。</param>
        private static async Task<string> PostMessagePackAsync<T>(
            string url,
            T body,
            ReadOnlyMemory<byte> secret,
            CancellationToken ct)
        {
            var bodyBytes = MessagePackSerializer.Serialize(body);
            var uri = new Uri(url);

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            AddSignatureHeaders(request, secret, "POST", uri.AbsolutePath, bodyBytes);
            request.Content = new ByteArrayContent(bodyBytes);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-msgpack");

            using var response = await s_httpClient.SendAsync(request, ct);
            return $"{(int)response.StatusCode} {response.StatusCode}";
        }

        /// <summary>
        /// HMAC-SHA256 署名ヘッダ（X-Signature / X-Timestamp / X-Nonce）をリクエストに付与する。
        /// <paramref name="secret"/> が空の場合は何もしない。
        /// </summary>
        /// <param name="request">ヘッダを付与する HTTP リクエスト。</param>
        /// <param name="secret">HMAC 署名用シークレット。</param>
        /// <param name="method">HTTP メソッド（例: "POST"）。</param>
        /// <param name="path">リクエストパス（クエリを含まない絶対パス）。</param>
        /// <param name="bodyBytes">シリアライズ済みリクエストボディ。</param>
        private static void AddSignatureHeaders(
            HttpRequestMessage request,
            ReadOnlyMemory<byte> secret,
            string method,
            string path,
            byte[] bodyBytes)
        {
            if (secret.IsEmpty) return;

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var nonce = Guid.NewGuid().ToString("N");
            var canonical = HmacRequestSigner.BuildCanonicalString(method, path, timestamp, nonce, bodyBytes);
            var signature = HmacRequestSigner.ComputeSignature(secret.ToArray(), canonical);

            request.Headers.Add("X-Signature", signature);
            request.Headers.Add("X-Timestamp", timestamp.ToString());
            request.Headers.Add("X-Nonce", nonce);
        }
    }
}
