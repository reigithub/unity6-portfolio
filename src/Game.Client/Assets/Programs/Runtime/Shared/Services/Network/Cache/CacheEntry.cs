using System;

namespace Game.Shared.Services.Network.Cache
{
    /// <summary>
    /// キャッシュエントリ
    /// データと有効期限を保持
    /// </summary>
    /// <typeparam name="T">キャッシュするデータの型</typeparam>
    public class CacheEntry<T>
    {
        /// <summary>
        /// キャッシュされたデータ
        /// </summary>
        public T Data { get; }

        /// <summary>
        /// キャッシュの有効期限（UTC）
        /// </summary>
        public DateTime ExpiresAt { get; }

        /// <summary>
        /// キャッシュが作成された時刻（UTC）
        /// </summary>
        public DateTime CreatedAt { get; }

        /// <summary>
        /// キャッシュが有効期限切れかどうか
        /// </summary>
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;

        /// <summary>
        /// キャッシュの残り有効時間
        /// </summary>
        public TimeSpan TimeToLive => IsExpired ? TimeSpan.Zero : ExpiresAt - DateTime.UtcNow;

        public CacheEntry(T data, TimeSpan? duration = null)
        {
            Data = data;
            CreatedAt = DateTime.UtcNow;
            ExpiresAt = duration.HasValue
                ? CreatedAt.Add(duration.Value)
                : DateTime.MaxValue;
        }

        public CacheEntry(T data, DateTime expiresAt)
        {
            Data = data;
            CreatedAt = DateTime.UtcNow;
            ExpiresAt = expiresAt;
        }
    }

    /// <summary>
    /// 内部ストレージ用の非ジェネリックキャッシュエントリ
    /// </summary>
    internal class CacheEntryInternal
    {
        public object Data { get; }
        public DateTime ExpiresAt { get; }
        public DateTime CreatedAt { get; }
        public Type DataType { get; }

        public bool IsExpired => DateTime.UtcNow > ExpiresAt;

        public CacheEntryInternal(object data, Type dataType, TimeSpan? duration = null)
        {
            Data = data;
            DataType = dataType;
            CreatedAt = DateTime.UtcNow;
            ExpiresAt = duration.HasValue
                ? CreatedAt.Add(duration.Value)
                : DateTime.MaxValue;
        }

        public CacheEntry<T> ToTyped<T>()
        {
            if (typeof(T) != DataType)
            {
                throw new InvalidCastException(
                    $"キャッシュの型が一致しません。期待: {typeof(T).Name}, 実際: {DataType.Name}");
            }

            return new CacheEntry<T>((T)Data, ExpiresAt);
        }
    }
}
