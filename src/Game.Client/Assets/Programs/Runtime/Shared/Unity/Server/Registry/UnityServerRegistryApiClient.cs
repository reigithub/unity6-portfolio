using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Game.Library.Shared.Dto;
using MessagePack;
using UnityEngine;
using VContainer;

namespace Game.Shared.Unity.Server
{
    /// <summary>
    /// <see cref="IUnityServerRegistryApiClient"/> の実装。
    /// Game.Server の <c>/api/unity-server/*</c> エンドポイントに対して HTTP 通信を行う。
    /// <c>static readonly HttpClient</c> でプロセス共有し、ソケット枯渇を防ぐ。
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
        public async Task<bool> RegisterAsync(string dsAddress, CancellationToken ct)
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
                    GamePort = config.GamePort,
                    HealthPort = config.HealthPort,
                };
                var url = $"{config.GameServerUrl}/api/unity-server/register";
                var status = await PostMessagePackAsync(url, request, config.AuthSecretKey, ct);
                Debug.Log($"[UnityServerRegistryApiClient] 自己登録完了: status={status}");
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
                var url = $"{config.GameServerUrl}/api/unity-server/heartbeat?dsId={Uri.EscapeDataString(config.DsId)}";
                var status = await PostAsync(url, config.AuthSecretKey, ct);
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
                var url = $"{config.GameServerUrl}/api/unity-server/deregister?dsId={Uri.EscapeDataString(config.DsId)}";
                var status = await PostAsync(url, config.AuthSecretKey, ct);
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
                var url = $"{config.GameServerUrl}/api/unity-server/session-ended"
                          + $"?dsId={Uri.EscapeDataString(config.DsId)}"
                          + $"&matchId={Uri.EscapeDataString(matchId ?? string.Empty)}";
                var status = await PostAsync(url, config.AuthSecretKey, ct);
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
        /// MessagePack 形式で body を送信する HTTP POST ヘルパー。
        /// <c>application/x-msgpack</c> Content-Type で送り、
        /// サーバー側の <c>MessagePackInputFormatter</c> により DTO に自動バインドされる。
        /// </summary>
        private static async Task<string> PostMessagePackAsync<T>(string url, T body, ReadOnlyMemory<byte> authSecretKey, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            AddAuthHeader(request, authSecretKey);

            var bodyBytes = MessagePackSerializer.Serialize(body);
            request.Content = new ByteArrayContent(bodyBytes);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-msgpack");

            using var response = await s_httpClient.SendAsync(request, ct);
            return $"{(int)response.StatusCode} {response.StatusCode}";
        }

        /// <summary>
        /// body なしの HTTP POST ヘルパー。<c>[FromQuery]</c> 方式の
        /// heartbeat / deregister / session-ended エンドポイント用。
        /// </summary>
        private static async Task<string> PostAsync(string url, ReadOnlyMemory<byte> authSecretKey, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            AddAuthHeader(request, authSecretKey);
            // Content は設定しない（null） → Content-Length: 0 で送信される

            using var response = await s_httpClient.SendAsync(request, ct);
            return $"{(int)response.StatusCode} {response.StatusCode}";
        }

        /// <summary>
        /// X-DS-Auth ヘッダを付与する共通処理。
        /// DefaultRequestHeaders は共有時の競合回避のため使わず、各リクエスト単位で設定する。
        /// </summary>
        private static void AddAuthHeader(HttpRequestMessage request, ReadOnlyMemory<byte> authSecretKey)
        {
            if (!authSecretKey.IsEmpty)
            {
                var authValue = Encoding.UTF8.GetString(authSecretKey.Span);
                request.Headers.Add("X-DS-Auth", authValue);
            }
        }
    }
}
