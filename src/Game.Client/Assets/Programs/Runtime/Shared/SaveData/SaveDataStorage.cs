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
    /// 物理I/O（排他制御・リトライ・アトミック書き込み）を集約する
    /// </summary>
    public class SaveDataStorage : ISaveDataStorage
    {
        private const string DefaultExtension = ".bin";
        private const string TempExtension = ".tmp";
        private const int MaxRetryCount = 3;
        private const int RetryDelayMs = 100;

        // Windowsにおける ERROR_SHARING_VIOLATION (0x20) のHRESULT
        private const int SharingViolationHResult = unchecked((int)0x80070020);

        // ファイルごとの排他制御用セマフォ（Save/Loadの競合防止）
        private readonly Dictionary<string, SemaphoreSlim> _fileLocks = new();
        private readonly object _lockDictionaryLock = new();

        public SaveDataStorage()
        {
        }

        public string BasePath => GameEnvironmentHelper.PersistentDataPath;

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

        public UniTask<T> LoadAsync<T>(string key) where T : class => LoadAsync<T>(key, default);

        public async UniTask<T> LoadAsync<T>(string key, T defaultValue) where T : class
        {
            var bytes = await LoadBytesAsync(key);
            if (bytes == null)
            {
                return defaultValue;
            }

            try
            {
                var data = MemoryPackSerializer.Deserialize<T>(bytes);
                return data ?? defaultValue;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveDataStorage] Failed to deserialize {key}: {e.Message}");
                return defaultValue;
            }
        }

        public async UniTask SaveAsync<T>(string key, T data) where T : class
        {
            var bytes = MemoryPackSerializer.Serialize(data);
            await SaveBytesAsync(key, bytes);
        }

        public async UniTask<byte[]> LoadBytesAsync(string key)
        {
            var path = GetFullPath(key);
            var fileLock = GetFileLock(key);
            await fileLock.WaitAsync();
            try
            {
                if (!File.Exists(path))
                {
                    Debug.Log($"[SaveDataStorage] File not found: {key}");
                    return null;
                }

                var bytes = await File.ReadAllBytesAsync(path);
                Debug.Log($"[SaveDataStorage] Loaded: {key} ({bytes.Length} bytes)");
                return bytes;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveDataStorage] Failed to load {key}: {e.Message}");
                return null;
            }
            finally
            {
                fileLock.Release();
            }
        }

        public async UniTask SaveBytesAsync(string key, byte[] data)
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

                var tmpPath = path + TempExtension;

                // WebGLではIndexedDBへの同時アクセス競合を防ぐためリトライと排他制御
                Exception lastException = null;
                for (int retry = 0; retry < MaxRetryCount; retry++)
                {
                    try
                    {
                        await WriteAtomicAsync(path, tmpPath, data);
                        Debug.Log($"[SaveDataStorage] Saved: {key} ({data.Length} bytes)");
                        return;
                    }
                    catch (IOException ex) when (IsSharingViolation(ex))
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
            catch (Exception e) when (!(e is IOException ioEx && IsSharingViolation(ioEx)))
            {
                Debug.LogError($"[SaveDataStorage] Failed to save {key}: {e.Message}");
                throw;
            }
            finally
            {
                fileLock.Release();
            }
        }

        // File.Replace/Moveがサポートされない環境（WebGL等）と一度判明したら、以後のセーブは直書きへ直行する
        // （毎回「tmp書き込み→PlatformNotSupportedException→直書き」の二重書き込みと例外コストを避けるためのラッチ）
        private static bool _atomicWriteUnsupported;

        /// <summary>
        /// 一時ファイルへ書き込んでから本ファイルへ置き換えるアトミック書き込み
        /// 書き込み途中でのプロセス強制終了によるファイル破損を防ぐ
        /// </summary>
        private static async UniTask WriteAtomicAsync(string path, string tmpPath, byte[] data)
        {
            if (_atomicWriteUnsupported)
            {
                await File.WriteAllBytesAsync(path, data);
                return;
            }

            try
            {
                await File.WriteAllBytesAsync(tmpPath, data);

                if (File.Exists(path))
                {
                    File.Replace(tmpPath, path, null);
                }
                else
                {
                    File.Move(tmpPath, path);
                }
            }
            catch (PlatformNotSupportedException)
            {
                // WebGL(IL2CPP/Emscripten)等、File.Replace/Moveが未対応の環境向けフォールバック（直接上書き）
                _atomicWriteUnsupported = true;
                await File.WriteAllBytesAsync(path, data);
            }
        }

        /// <summary>
        /// 例外がファイル共有違反（他プロセス/スレッドによるロック）によるものか判定する
        /// </summary>
        private static bool IsSharingViolation(IOException ex)
        {
            if (ex.HResult == SharingViolationHResult)
            {
                return true;
            }

            // HResultが取得できないプラットフォーム向けのフォールバック判定
            return ex.Message.Contains("Sharing violation");
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
