using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using MemoryPack;
using UnityEngine;

namespace Game.Shared.SaveData
{
    /// <summary>
    /// MemoryPackを使用したセーブデータストレージ実装
    /// Application.persistentDataPath配下にバイナリファイルとして保存
    /// </summary>
    public class SaveDataStorage : ISaveDataStorage
    {
        private const string DefaultExtension = ".bin";
        private const int MaxRetryCount = 3;
        private const int RetryDelayMs = 100;

        // ファイルごとの排他制御用セマフォ
        private readonly Dictionary<string, SemaphoreSlim> _fileLocks = new();
        private readonly object _lockDictionaryLock = new();

        public SaveDataStorage()
        {
        }

        private SemaphoreSlim GetFileLock(string key)
        {
            lock (_lockDictionaryLock)
            {
                if (!_fileLocks.TryGetValue(key, out var semaphore))
                {
                    semaphore = new SemaphoreSlim(1, 1);
                    _fileLocks[key] = semaphore;
                }

                return semaphore;
            }
        }

        public string BasePath => Application.persistentDataPath;

        public async UniTask<T> LoadAsync<T>(string key) where T : class
        {
            return await LoadAsync<T>(key, default);
        }

        public async UniTask<T> LoadAsync<T>(string key, T defaultValue) where T : class
        {
            var path = GetFullPath(key);

            try
            {
                if (!File.Exists(path))
                {
                    Debug.Log($"[SaveDataStorage] File not found: {key}");
                    return defaultValue;
                }

                var bytes = await File.ReadAllBytesAsync(path);
                var data = MemoryPackSerializer.Deserialize<T>(bytes);

                Debug.Log($"[SaveDataStorage] Loaded: {key} ({bytes.Length} bytes)");
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveDataStorage] Failed to load {key}: {e.Message}");
                return defaultValue;
            }
        }

        public async UniTask SaveAsync<T>(string key, T data) where T : class
        {
            var path = GetFullPath(key);
            var fileLock = GetFileLock(key);
            await fileLock.WaitAsync();
            try
            {
                // ディレクトリが存在しない場合は作成
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var bytes = MemoryPackSerializer.Serialize(data);

                // WebGLではIndexedDBへの同時アクセス競合を防ぐためリトライと排他制御
                Exception lastException = null;
                for (int retry = 0; retry < MaxRetryCount; retry++)
                {
                    try
                    {
                        await File.WriteAllBytesAsync(path, bytes);
                        Debug.Log($"[SaveDataStorage] Saved: {key} ({bytes.Length} bytes)");
                        return;
                    }
                    catch (IOException ex) when (ex.Message.Contains("Sharing violation"))
                    {
                        lastException = ex;
                        Debug.LogWarning($"[SaveDataStorage] Retry {retry + 1}/{MaxRetryCount} for {key}: {ex.Message}");
                        await UniTask.Delay(RetryDelayMs * (retry + 1));
                    }
                }

                // リトライ後も失敗した場合
                if (lastException != null)
                {
                    Debug.LogError($"[SaveDataStorage] Failed to save {key} after {MaxRetryCount} retries: {lastException.Message}");
                    throw lastException;
                }
            }
            catch (Exception e) when (!e.Message.Contains("Sharing violation"))
            {
                Debug.LogError($"[SaveDataStorage] Failed to save {key}: {e.Message}");
                throw;
            }
            finally
            {
                fileLock.Release();
            }
        }

        public async UniTask DeleteAsync(string key)
        {
            var path = GetFullPath(key);

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    Debug.Log($"[SaveDataStorage] Deleted: {key}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveDataStorage] Failed to delete {key}: {e.Message}");
                throw;
            }

            await UniTask.CompletedTask;
        }

        public bool Exists(string key)
        {
            var path = GetFullPath(key);
            return File.Exists(path);
        }

        public string GetFullPath(string key)
        {
            // 拡張子がない場合は.binを付与
            if (!Path.HasExtension(key))
            {
                key += DefaultExtension;
            }

            return Path.Combine(BasePath, key);
        }
    }
}