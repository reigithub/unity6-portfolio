using System;
using System.IO;
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

        public SaveDataStorage()
        {
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

            try
            {
                // ディレクトリが存在しない場合は作成
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var bytes = MemoryPackSerializer.Serialize(data);
                await File.WriteAllBytesAsync(path, bytes);

                Debug.Log($"[SaveDataStorage] Saved: {key} ({bytes.Length} bytes)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveDataStorage] Failed to save {key}: {e.Message}");
                throw;
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