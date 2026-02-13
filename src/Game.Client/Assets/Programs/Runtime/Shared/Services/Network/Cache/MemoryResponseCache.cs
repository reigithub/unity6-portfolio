using System;
using System.Collections.Concurrent;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Shared.Services.Network.Cache
{
    /// <summary>
    /// メモリベースのレスポンスキャッシュ実装
    /// </summary>
    public class MemoryResponseCache : IResponseCache
    {
        private readonly ConcurrentDictionary<string, CacheEntryInternal> _cache;
        private readonly int _maxEntries;

        public int Count => _cache.Count;

        public MemoryResponseCache() : this(1000)
        {
        }

        public MemoryResponseCache(int maxEntries)
        {
            _maxEntries = maxEntries;
            _cache = new ConcurrentDictionary<string, CacheEntryInternal>();
        }

        public UniTask<CacheEntry<T>> GetAsync<T>(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return UniTask.FromResult<CacheEntry<T>>(null);
            }

            if (!_cache.TryGetValue(key, out var entry))
            {
                return UniTask.FromResult<CacheEntry<T>>(null);
            }

            // 期限切れチェック
            if (entry.IsExpired)
            {
                _cache.TryRemove(key, out _);
                return UniTask.FromResult<CacheEntry<T>>(null);
            }

            // 型チェック
            try
            {
                var typedEntry = entry.ToTyped<T>();
                return UniTask.FromResult(typedEntry);
            }
            catch (InvalidCastException ex)
            {
                Debug.LogWarning($"[MemoryResponseCache] Type mismatch for key '{key}': {ex.Message}");
                return UniTask.FromResult<CacheEntry<T>>(null);
            }
        }

        public UniTask SetAsync<T>(string key, T data, TimeSpan? duration = null)
        {
            if (string.IsNullOrEmpty(key))
            {
                return UniTask.CompletedTask;
            }

            // キャッシュサイズ制限チェック
            if (_cache.Count >= _maxEntries && !_cache.ContainsKey(key))
            {
                CleanupOldestEntries();
            }

            var entry = new CacheEntryInternal(data, typeof(T), duration);
            _cache.AddOrUpdate(key, entry, (_, _) => entry);

            return UniTask.CompletedTask;
        }

        public UniTask RemoveAsync(string key)
        {
            if (!string.IsNullOrEmpty(key))
            {
                _cache.TryRemove(key, out _);
            }

            return UniTask.CompletedTask;
        }

        public UniTask ClearAsync()
        {
            _cache.Clear();
            return UniTask.CompletedTask;
        }

        public UniTask CleanupExpiredAsync()
        {
            var expiredKeys = _cache
                .Where(kvp => kvp.Value.IsExpired)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
            }

            if (expiredKeys.Count > 0)
            {
                Debug.Log($"[MemoryResponseCache] Cleaned up {expiredKeys.Count} expired entries");
            }

            return UniTask.CompletedTask;
        }

        public bool Contains(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            if (!_cache.TryGetValue(key, out var entry))
            {
                return false;
            }

            // 期限切れの場合はfalse
            if (entry.IsExpired)
            {
                _cache.TryRemove(key, out _);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 最も古いエントリを削除してスペースを確保
        /// </summary>
        private void CleanupOldestEntries()
        {
            // まず期限切れを削除
            CleanupExpiredAsync().Forget();

            // それでもオーバーしている場合は最も古いものを削除
            if (_cache.Count >= _maxEntries)
            {
                var entriesToRemove = _cache
                    .OrderBy(kvp => kvp.Value.CreatedAt)
                    .Take(_cache.Count - _maxEntries + 10) // 少し余裕を持って削除
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in entriesToRemove)
                {
                    _cache.TryRemove(key, out _);
                }

                Debug.Log($"[MemoryResponseCache] Removed {entriesToRemove.Count} oldest entries due to size limit");
            }
        }
    }
}
