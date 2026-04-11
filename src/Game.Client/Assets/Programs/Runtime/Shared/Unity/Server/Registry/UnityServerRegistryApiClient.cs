using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
                var body = BuildRegistrationJson(config.DsId, dsAddress, config.GamePort, config.HealthPort);
                var url = $"{config.GameServerUrl}/api/unity-server/register";
                var status = await PostAsync(url, body, config.AuthSecretKey, ct);
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
                var status = await PostAsync(url, "{}", config.AuthSecretKey, ct);
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
                var status = await PostAsync(url, "{}", config.AuthSecretKey, ct);
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
                var status = await PostAsync(url, "{}", config.AuthSecretKey, ct);
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

        private static async Task<string> PostAsync(
            string url,
            string jsonBody,
            ReadOnlyMemory<byte> authSecretKey,
            CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);

            // X-DS-Auth ヘッダーは各リクエストに個別付与（DefaultRequestHeaders は使わない）
            if (!authSecretKey.IsEmpty)
            {
                var authValue = Encoding.UTF8.GetString(authSecretKey.Span);
                request.Headers.Add("X-DS-Auth", authValue);
            }

            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var response = await s_httpClient.SendAsync(request, ct);
            return $"{(int)response.StatusCode} {response.StatusCode}";
        }

        private static string BuildRegistrationJson(string dsId, string dsAddress, ushort gamePort, int healthPort)
        {
            return $"{{\"dsId\":\"{EscapeJson(dsId)}\","
                   + $"\"address\":\"{EscapeJson(dsAddress)}\","
                   + $"\"gamePort\":{gamePort},"
                   + $"\"healthPort\":{healthPort}}}";
        }

        private static string EscapeJson(string value)
        {
            if (value == null)
                return string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }
    }
}
