using System;
using Cysharp.Threading.Tasks;

namespace Game.Shared.Services.Network.Cache
{
    /// <summary>
    /// レスポンスキャッシュのインターフェース
    /// </summary>
    public interface IResponseCache
    {
        /// <summary>
        /// キャッシュからデータを取得
        /// </summary>
        /// <typeparam name="T">データの型</typeparam>
        /// <param name="key">キャッシュキー</param>
        /// <returns>キャッシュエントリ（存在しないか期限切れの場合はnull）</returns>
        UniTask<CacheEntry<T>> GetAsync<T>(string key);

        /// <summary>
        /// キャッシュにデータを設定
        /// </summary>
        /// <typeparam name="T">データの型</typeparam>
        /// <param name="key">キャッシュキー</param>
        /// <param name="data">キャッシュするデータ</param>
        /// <param name="duration">有効期間（省略時は無期限）</param>
        UniTask SetAsync<T>(string key, T data, TimeSpan? duration = null);

        /// <summary>
        /// 指定したキーのキャッシュを削除
        /// </summary>
        /// <param name="key">キャッシュキー</param>
        UniTask RemoveAsync(string key);

        /// <summary>
        /// 全キャッシュをクリア
        /// </summary>
        UniTask ClearAsync();

        /// <summary>
        /// 期限切れのキャッシュを削除
        /// </summary>
        UniTask CleanupExpiredAsync();

        /// <summary>
        /// 指定したキーのキャッシュが存在するかチェック（期限切れは含まない）
        /// </summary>
        /// <param name="key">キャッシュキー</param>
        /// <returns>有効なキャッシュが存在する場合はtrue</returns>
        bool Contains(string key);

        /// <summary>
        /// 現在のキャッシュエントリ数
        /// </summary>
        int Count { get; }
    }
}
