using System;
using System.Collections.Generic;
using Game.Shared.Services.Network.Policies;

namespace Game.Shared.Services.Network.Models
{
    /// <summary>
    /// APIリクエストオプション
    /// 個々のリクエストに対する設定をカスタマイズ
    /// </summary>
    public sealed class RequestOptions
    {
        /// <summary>
        /// リトライポリシー
        /// </summary>
        public RetryPolicy RetryPolicy { get; set; }

        /// <summary>
        /// タイムアウト秒数（nullの場合はデフォルト値を使用）
        /// </summary>
        public int? TimeoutSeconds { get; set; }

        /// <summary>
        /// キャッシュを使用するかどうか
        /// </summary>
        public bool UseCache { get; set; }

        /// <summary>
        /// キャッシュの有効期間（UseCache=trueの場合に使用）
        /// </summary>
        public TimeSpan? CacheDuration { get; set; }

        /// <summary>
        /// キャッシュキーのプレフィックス（空の場合はエンドポイントをそのまま使用）
        /// </summary>
        public string CacheKeyPrefix { get; set; }

        /// <summary>
        /// オフライン時にキャッシュからフォールバックするかどうか
        /// </summary>
        public bool FallbackToCache { get; set; } = true;

        /// <summary>
        /// 追加のHTTPヘッダー
        /// </summary>
        public Dictionary<string, string> AdditionalHeaders { get; set; }

        /// <summary>
        /// デフォルトのリクエストオプション
        /// </summary>
        public static RequestOptions Default => new()
        {
            RetryPolicy = Policies.RetryPolicy.Default,
            UseCache = false,
            FallbackToCache = true
        };

        /// <summary>
        /// リトライなしのリクエストオプション
        /// </summary>
        public static RequestOptions NoRetry => new()
        {
            RetryPolicy = Policies.RetryPolicy.None,
            UseCache = false,
            FallbackToCache = true
        };

        /// <summary>
        /// キャッシュ有効のリクエストオプション
        /// </summary>
        /// <param name="duration">キャッシュ有効期間</param>
        /// <returns>キャッシュ有効のRequestOptions</returns>
        public static RequestOptions WithCache(TimeSpan duration) => new()
        {
            RetryPolicy = Policies.RetryPolicy.Default,
            UseCache = true,
            CacheDuration = duration,
            FallbackToCache = true
        };

        /// <summary>
        /// カスタムタイムアウトのリクエストオプション
        /// </summary>
        /// <param name="timeoutSeconds">タイムアウト秒数</param>
        /// <returns>カスタムタイムアウトのRequestOptions</returns>
        public static RequestOptions WithTimeout(int timeoutSeconds) => new()
        {
            RetryPolicy = Policies.RetryPolicy.Default,
            TimeoutSeconds = timeoutSeconds,
            UseCache = false,
            FallbackToCache = true
        };

        /// <summary>
        /// 有効なリトライポリシーを取得（nullの場合はデフォルトを返す）
        /// </summary>
        public RetryPolicy GetEffectiveRetryPolicy()
        {
            return RetryPolicy ?? Policies.RetryPolicy.Default;
        }

        /// <summary>
        /// 有効なタイムアウト秒数を取得
        /// </summary>
        /// <param name="defaultTimeout">デフォルトのタイムアウト秒数</param>
        public int GetEffectiveTimeout(int defaultTimeout)
        {
            return TimeoutSeconds ?? defaultTimeout;
        }
    }
}
