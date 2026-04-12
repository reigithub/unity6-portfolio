using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Shared.Environment;
using UnityEngine;

namespace Game.Shared.Unity.Server
{
    /// <summary>
    /// CLI 引数・環境変数・GCE メタデータから <see cref="UnityServerConfig"/> を構築するファクトリ。
    /// </summary>
    public static class UnityServerConfigFactory
    {
        /// <summary>
        /// CLI 引数・環境変数・GCE メタデータから設定を非同期で構築する。
        /// GCE 外部 IP 取得には最大 2 秒を要する。
        /// </summary>
        /// <param name="ct">キャンセルトークン。</param>
        /// <returns>構築済みの <see cref="UnityServerConfig"/>。</returns>
        public static async UniTask<UnityServerConfig> BuildAsync(CancellationToken ct)
        {
            var dsId = $"ds-{Guid.NewGuid():N}";

            var args = ClArgsHelper.Parse();
            var gamePort = ParsePort(args);
            var healthPort = ParseHealthPort(args);
            var gameServerUrl = ParseGameServerUrl();
            ValidateGameServerUrl(gameServerUrl);
            var authSecretBytes = ParseSecret();

            // GCE 環境の場合は外部 IP を取得（非 GCE では 2 秒 timeout で null）
            var envPublicAddress = EnvVarHelper.Get(EnvVarKeys.PublicAddress);
            var publicAddress = !string.IsNullOrEmpty(envPublicAddress)
                ? envPublicAddress
                : await GceMetadataDetector.TryFetchExternalIpAsync(ct);

            var authSecretKey = authSecretBytes != null
                ? new ReadOnlyMemory<byte>(authSecretBytes)
                : ReadOnlyMemory<byte>.Empty;

            return new UnityServerConfig(
                dsId,
                gameServerUrl,
                gamePort,
                healthPort,
                authSecretKey,
                publicAddress);
        }

        /// <summary>
        /// Fusion UDP ポートを解決する。優先順位: CLI 引数 → 環境変数 UNITY_SERVER_PORT → デフォルト 7777。
        /// </summary>
        private static ushort ParsePort(Dictionary<string, string> args)
        {
            if (ClArgsHelper.TryGet(args, "--port", out ushort cliPort, p => ushort.Parse(p)))
                return cliPort;

            if (EnvVarHelper.TryGet(EnvVarKeys.UnityServerPort, out ushort envPort, p => ushort.Parse(p)))
                return envPort;

            return 7777;
        }

        /// <summary>
        /// ヘルスチェック TCP ポートを解決する。優先順位: CLI 引数 → 環境変数 UNITY_SERVER_HEALTH_PORT → デフォルト 7778。
        /// </summary>
        private static int ParseHealthPort(Dictionary<string, string> args)
        {
            if (ClArgsHelper.TryGet(args, "--health-port", out int cliPort, p => int.Parse(p)))
                return cliPort;

            if (EnvVarHelper.TryGet(EnvVarKeys.UnityServerHealthPort, out int envPort, p => int.Parse(p)))
                return envPort;

            return 7778;
        }

        /// <summary>
        /// 環境変数 UNITY_SERVER_AUTH_SESSION_SECRET から HMAC シークレットを取得する。
        /// </summary>
        private static byte[] ParseSecret()
        {
            EnvVarHelper.TryGet(EnvVarKeys.UnityServerAuthSecretKey, out byte[] secret, s => Encoding.UTF8.GetBytes(s));
            return secret;
        }

        /// <summary>
        /// 環境変数 GAME_SERVER_URL から Game.Server URL を取得する。
        /// 未設定時は null を返す。
        /// </summary>
        private static string ParseGameServerUrl()
        {
            EnvVarHelper.TryGet(EnvVarKeys.GameServerUrl, out string url, u => u.TrimEnd('/'));
            return url;
        }

        /// <summary>
        /// GAME_SERVER_URL の TLS スキーマを検証する。
        /// 非ローカルホストに対して <c>http://</c> が指定された場合は起動を中止する。
        /// </summary>
        /// <param name="url">検証対象の URL。null/空なら検証スキップ (自己登録スキップの既存動作)。</param>
        /// <exception cref="InvalidOperationException">絶対 URI でない、または非ローカル host に対して http が指定された場合。</exception>
        private static void ValidateGameServerUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                throw new InvalidOperationException(
                    $"GAME_SERVER_URL is not a valid absolute URI: {url}");

            if (uri.Scheme == Uri.UriSchemeHttps) return;

            if (uri.Scheme == Uri.UriSchemeHttp && IsLocalHost(uri.Host))
            {
                Debug.LogWarning(
                    $"[UnityServerConfigFactory] HTTP scheme allowed only for local host: {url}");
                return;
            }

            throw new InvalidOperationException(
                $"GAME_SERVER_URL must use https:// in non-local environments, got: {url}");
        }

        /// <summary>
        /// 指定ホスト名がローカル扱いかどうかを判定する。
        /// </summary>
        private static bool IsLocalHost(string host) =>
            string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            host == "127.0.0.1" ||
            host == "::1" ||
            string.Equals(host, "host.docker.internal", StringComparison.OrdinalIgnoreCase);
    }
}
