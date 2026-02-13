using System;
using System.Collections.Generic;

namespace Game.Shared.Services.Network.Policies
{
    /// <summary>
    /// リトライポリシー設定
    /// 指数バックオフによるリトライ戦略を定義
    /// </summary>
    public sealed class RetryPolicy
    {
        /// <summary>
        /// 最大リトライ回数
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// 初期遅延時間（ミリ秒）
        /// </summary>
        public int InitialDelayMs { get; set; } = 1000;

        /// <summary>
        /// 最大遅延時間（ミリ秒）
        /// </summary>
        public int MaxDelayMs { get; set; } = 30000;

        /// <summary>
        /// バックオフ乗数
        /// </summary>
        public double BackoffMultiplier { get; set; } = 2.0;

        /// <summary>
        /// リトライ対象のHTTPステータスコード
        /// </summary>
        public HashSet<int> RetryableStatusCodes { get; set; } = new()
        {
            408, // Request Timeout
            429, // Too Many Requests
            500, // Internal Server Error
            502, // Bad Gateway
            503, // Service Unavailable
            504  // Gateway Timeout
        };

        /// <summary>
        /// デフォルトのリトライポリシー
        /// </summary>
        public static RetryPolicy Default => new();

        /// <summary>
        /// リトライを行わないポリシー
        /// </summary>
        public static RetryPolicy None => new() { MaxRetries = 0 };

        /// <summary>
        /// 積極的なリトライポリシー（回数多め・遅延短め）
        /// </summary>
        public static RetryPolicy Aggressive => new()
        {
            MaxRetries = 5,
            InitialDelayMs = 500,
            MaxDelayMs = 10000,
            BackoffMultiplier = 1.5
        };

        /// <summary>
        /// 指定されたリトライ回数に対する遅延時間を計算
        /// </summary>
        /// <param name="retryAttempt">リトライ回数（0始まり）</param>
        /// <returns>遅延時間（ミリ秒）</returns>
        public int GetDelayMs(int retryAttempt)
        {
            if (retryAttempt < 0)
                throw new ArgumentOutOfRangeException(nameof(retryAttempt), "リトライ回数は0以上である必要があります");

            var delay = InitialDelayMs * Math.Pow(BackoffMultiplier, retryAttempt);
            return (int)Math.Min(delay, MaxDelayMs);
        }

        /// <summary>
        /// 指定されたステータスコードがリトライ対象かどうかを判定
        /// </summary>
        /// <param name="statusCode">HTTPステータスコード</param>
        /// <returns>リトライ対象の場合はtrue</returns>
        public bool IsRetryableStatusCode(int statusCode)
        {
            return RetryableStatusCodes.Contains(statusCode);
        }

        /// <summary>
        /// 指定されたリトライ回数がまだリトライ可能かどうかを判定
        /// </summary>
        /// <param name="currentAttempt">現在のリトライ回数（0始まり）</param>
        /// <returns>リトライ可能な場合はtrue</returns>
        public bool CanRetry(int currentAttempt)
        {
            return currentAttempt < MaxRetries;
        }
    }
}
