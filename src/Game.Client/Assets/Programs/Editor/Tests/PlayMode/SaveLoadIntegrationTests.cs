using System;
using System.Collections;
using System.IO;
using Cysharp.Threading.Tasks;
using MemoryPack;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// セーブ/ロード機能の統合テスト
    /// 実際のファイルI/Oを含むPlayModeテスト
    /// </summary>
    [TestFixture]
    public class SaveLoadIntegrationTests
    {
        private string _testSaveDirectory;
        private const string TestFileName = "PlayModeTestSaveData";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // テスト用の一時ディレクトリを作成
            _testSaveDirectory = Path.Combine(Application.temporaryCachePath, "PlayModeTestSaves");
            if (!Directory.Exists(_testSaveDirectory))
            {
                Directory.CreateDirectory(_testSaveDirectory);
            }
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // テストファイルをクリーンアップ
            yield return CleanupTestFiles().ToCoroutine();
        }

        /// <summary>
        /// データの保存と読み込みが正常に動作することを確認
        /// </summary>
        [UnityTest]
        public IEnumerator SaveAndLoad_RoundTrip_PreservesData()
        {
            return UniTask.ToCoroutine(async () =>
            {
                // Arrange
                var originalData = new TestSaveData
                {
                    PlayerName = "TestPlayer",
                    Score = 12345,
                    PlayTime = 3600.5f,
                    LastPlayed = DateTime.UtcNow
                };

                var filePath = GetTestFilePath("roundtrip_test");

                // Act - Save
                await SaveDataAsync(filePath, originalData);

                // Assert - File exists
                Assert.IsTrue(File.Exists(filePath), "Save file should exist");

                // Act - Load
                var loadedData = await LoadDataAsync<TestSaveData>(filePath);

                // Assert - Data preserved
                Assert.IsNotNull(loadedData, "Loaded data should not be null");
                Assert.AreEqual(originalData.PlayerName, loadedData.PlayerName);
                Assert.AreEqual(originalData.Score, loadedData.Score);
                Assert.AreEqual(originalData.PlayTime, loadedData.PlayTime, 0.001f);
            });
        }

        /// <summary>
        /// 存在しないファイルの読み込みがnullを返すことを確認
        /// </summary>
        [UnityTest]
        public IEnumerator Load_NonExistentFile_ReturnsNull()
        {
            return UniTask.ToCoroutine(async () =>
            {
                // Arrange
                var nonExistentPath = GetTestFilePath("non_existent_file");

                // Act
                var loadedData = await LoadDataAsync<TestSaveData>(nonExistentPath);

                // Assert
                Assert.IsNull(loadedData, "Loading non-existent file should return null");
            });
        }

        /// <summary>
        /// データの上書き保存が正常に動作することを確認
        /// </summary>
        [UnityTest]
        public IEnumerator Save_OverwriteExisting_UpdatesData()
        {
            return UniTask.ToCoroutine(async () =>
            {
                // Arrange
                var filePath = GetTestFilePath("overwrite_test");

                var originalData = new TestSaveData { Score = 100 };
                await SaveDataAsync(filePath, originalData);

                var updatedData = new TestSaveData { Score = 200 };

                // Act
                await SaveDataAsync(filePath, updatedData);
                var loadedData = await LoadDataAsync<TestSaveData>(filePath);

                // Assert
                Assert.AreEqual(200, loadedData.Score, "Data should be overwritten");
            });
        }

        /// <summary>
        /// ファイル削除が正常に動作することを確認
        /// </summary>
        [UnityTest]
        public IEnumerator Delete_ExistingFile_RemovesFile()
        {
            return UniTask.ToCoroutine(async () =>
            {
                // Arrange
                var filePath = GetTestFilePath("delete_test");
                var data = new TestSaveData { Score = 100 };
                await SaveDataAsync(filePath, data);

                Assert.IsTrue(File.Exists(filePath), "File should exist before delete");

                // Act
                await DeleteDataAsync(filePath);

                // Assert
                Assert.IsFalse(File.Exists(filePath), "File should be deleted");
            });
        }

        /// <summary>
        /// 大きなデータの保存/読み込みが正常に動作することを確認
        /// </summary>
        [UnityTest]
        public IEnumerator SaveAndLoad_LargeData_Works()
        {
            return UniTask.ToCoroutine(async () =>
            {
                // Arrange
                var largeData = new TestSaveDataWithArray
                {
                    Scores = new int[10000]
                };
                for (int i = 0; i < largeData.Scores.Length; i++)
                {
                    largeData.Scores[i] = i * 2;
                }

                var filePath = GetTestFilePath("large_data_test");

                // Act
                await SaveDataAsync(filePath, largeData);
                var loadedData = await LoadDataAsync<TestSaveDataWithArray>(filePath);

                // Assert
                Assert.IsNotNull(loadedData);
                Assert.AreEqual(largeData.Scores.Length, loadedData.Scores.Length);
                Assert.AreEqual(largeData.Scores[0], loadedData.Scores[0]);
                Assert.AreEqual(largeData.Scores[9999], loadedData.Scores[9999]);
            });
        }

        /// <summary>
        /// 複数ファイルの同時保存/読み込みが正常に動作することを確認
        /// </summary>
        [UnityTest]
        public IEnumerator SaveAndLoad_MultipleFiles_Independent()
        {
            return UniTask.ToCoroutine(async () =>
            {
                // Arrange
                var data1 = new TestSaveData { PlayerName = "Player1", Score = 100 };
                var data2 = new TestSaveData { PlayerName = "Player2", Score = 200 };

                var filePath1 = GetTestFilePath("multi_test_1");
                var filePath2 = GetTestFilePath("multi_test_2");

                // Act
                await SaveDataAsync(filePath1, data1);
                await SaveDataAsync(filePath2, data2);

                var loaded1 = await LoadDataAsync<TestSaveData>(filePath1);
                var loaded2 = await LoadDataAsync<TestSaveData>(filePath2);

                // Assert
                Assert.AreEqual("Player1", loaded1.PlayerName);
                Assert.AreEqual(100, loaded1.Score);
                Assert.AreEqual("Player2", loaded2.PlayerName);
                Assert.AreEqual(200, loaded2.Score);
            });
        }

        /// <summary>
        /// 特殊文字を含むデータの保存/読み込みが正常に動作することを確認
        /// </summary>
        [UnityTest]
        public IEnumerator SaveAndLoad_SpecialCharacters_Preserved()
        {
            return UniTask.ToCoroutine(async () =>
            {
                // Arrange
                var data = new TestSaveData
                {
                    PlayerName = "日本語テスト🎮プレイヤー<>&\"'"
                };

                var filePath = GetTestFilePath("special_chars_test");

                // Act
                await SaveDataAsync(filePath, data);
                var loadedData = await LoadDataAsync<TestSaveData>(filePath);

                // Assert
                Assert.AreEqual(data.PlayerName, loadedData.PlayerName);
            });
        }

        /// <summary>
        /// 並列書き込みが安全に処理されることを確認
        /// 注: 同一ファイルへの同時書き込みはOSによって動作が異なるため、
        /// 異なるファイルへの並列書き込みをテスト
        /// </summary>
        [UnityTest]
        public IEnumerator ConcurrentSaves_DoNotCorruptData()
        {
            return UniTask.ToCoroutine(async () =>
            {
                // Arrange - 各タスクに異なるファイルを使用（OS間のファイルロック差異を回避）
                var uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
                const int taskCount = 10;
                var tasks = new UniTask[taskCount];
                var filePaths = new string[taskCount];

                // Act - 異なるファイルへ並列保存
                for (int i = 0; i < taskCount; i++)
                {
                    var data = new TestSaveData { Score = i, PlayerName = $"Player{i}" };
                    filePaths[i] = GetTestFilePath($"concurrent_test_{uniqueId}_{i}");
                    tasks[i] = SaveDataAsync(filePaths[i], data);
                }

                await UniTask.WhenAll(tasks);

                // Assert - 全ファイルが正常に保存されていることを確認
                for (int i = 0; i < taskCount; i++)
                {
                    var loadedData = await LoadDataAsync<TestSaveData>(filePaths[i]);
                    Assert.IsNotNull(loadedData, $"Data for file {i} should not be null");
                    Assert.AreEqual(i, loadedData.Score, $"Score for file {i} should match");
                    Assert.AreEqual($"Player{i}", loadedData.PlayerName, $"PlayerName for file {i} should match");
                }

                // クリーンアップ
                foreach (var path in filePaths)
                {
                    try
                    {
                        if (File.Exists(path))
                        {
                            File.Delete(path);
                        }
                    }
                    catch
                    {
                        // クリーンアップ失敗は無視
                    }
                }
            });
        }

        #region Helper Methods

        private string GetTestFilePath(string fileName)
        {
            return Path.Combine(_testSaveDirectory, $"{fileName}.dat");
        }

        private async UniTask SaveDataAsync<T>(string filePath, T data) where T : class
        {
            var bytes = MemoryPackSerializer.Serialize(data);
            await File.WriteAllBytesAsync(filePath, bytes);
        }

        private async UniTask<T> LoadDataAsync<T>(string filePath) where T : class
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var bytes = await File.ReadAllBytesAsync(filePath);
            return MemoryPackSerializer.Deserialize<T>(bytes);
        }

        private UniTask DeleteDataAsync(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            return UniTask.CompletedTask;
        }

        private async UniTask SaveDataWithRetryAsync<T>(string filePath, T data, int maxRetries = 3) where T : class
        {
            var bytes = MemoryPackSerializer.Serialize(data);

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    await File.WriteAllBytesAsync(filePath, bytes);
                    return;
                }
                catch (IOException) when (i < maxRetries - 1)
                {
                    // ファイルがロックされている場合はリトライ
                    await UniTask.Delay(10 * (i + 1));
                }
            }
        }

        private async UniTask<T> LoadDataWithRetryAsync<T>(string filePath, int maxRetries = 3) where T : class
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    var bytes = await File.ReadAllBytesAsync(filePath);
                    return MemoryPackSerializer.Deserialize<T>(bytes);
                }
                catch (IOException) when (i < maxRetries - 1)
                {
                    // ファイルがロックされている場合はリトライ
                    await UniTask.Delay(10 * (i + 1));
                }
            }

            return null;
        }

        private async UniTask CleanupTestFiles()
        {
            if (!Directory.Exists(_testSaveDirectory))
            {
                return;
            }

            // リトライロジックでクリーンアップ（ファイルロック対策）
            const int maxRetries = 3;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    // 個別ファイルを先に削除
                    foreach (var file in Directory.GetFiles(_testSaveDirectory))
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch
                        {
                            // 個別ファイル削除失敗は無視
                        }
                    }

                    // ディレクトリ削除
                    Directory.Delete(_testSaveDirectory, true);
                    return;
                }
                catch (Exception e)
                {
                    if (i == maxRetries - 1)
                    {
                        Debug.LogWarning($"[SaveLoadIntegrationTests] Failed to cleanup after {maxRetries} retries: {e.Message}");
                    }
                    else
                    {
                        await UniTask.Delay(50 * (i + 1));
                    }
                }
            }
        }

        #endregion


    }

    #region Test Data Classes

    [MemoryPackable]
    public partial class TestSaveData
    {
        public string PlayerName { get; set; } = "";
        public int Score { get; set; }
        public float PlayTime { get; set; }
        public DateTime LastPlayed { get; set; }
    }

    [MemoryPackable]
    public partial class TestSaveDataWithArray
    {
        public int[] Scores { get; set; } = Array.Empty<int>();
    }

    #endregion
}
