using System;
using System.Net.Http;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Shared.Unity.Server
{
    /// <summary>
    /// GCE (Google Compute Engine) のメタデータサーバーから外部 IP を取得する。
    /// 非 GCE 環境では 2 秒 timeout で silent fail し、null を返す。
    /// </summary>
    public static class GceMetadataDetector
    {
        private static readonly HttpClient s_client = new HttpClient();

        private const string MetadataUrl =
            "http://metadata.google.internal/computeMetadata/v1/instance/network-interfaces/0/access-configs/0/external-ip";

        /// <summary>
        /// GCE メタデータサーバーから外部 IP を非同期で取得する。
        /// 非 GCE 環境または 2 秒以内に応答がない場合は null を返す。
        /// </summary>
        /// <param name="ct">キャンセルトークン。</param>
        /// <returns>外部 IP 文字列。失敗時は null。</returns>
        public static async UniTask<string> TryFetchExternalIpAsync(CancellationToken ct)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(2));

                using var req = new HttpRequestMessage(HttpMethod.Get, MetadataUrl);
                req.Headers.Add("Metadata-Flavor", "Google");

                using var res = await s_client.SendAsync(req, cts.Token);
                if (!res.IsSuccessStatusCode)
                    return null;

                var ip = await res.Content.ReadAsStringAsync();
                return ip?.Trim();
            }
            catch
            {
                // 非 GCE 環境では timeout / 名前解決失敗で null を返す
                return null;
            }
        }
    }
}
