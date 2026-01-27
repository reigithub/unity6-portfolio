using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Shared.SaveData;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Shared
{
    [TestFixture]
    public class SaveServiceTests
    {
        #region Test Data and Mock Service

        private class TestSaveData
        {
            public int Version { get; set; } = 1;
            public string Name { get; set; } = "";
            public int Score { get; set; }
        }

        private class TestSaveService : SaveServiceBase<TestSaveData>
        {
            public const string TestSaveKey = "test_save";

            protected override string SaveKey => TestSaveKey;

            public int TestCurrentVersion { get; set; } = 1;
            protected override int CurrentVersion => TestCurrentVersion;

            public bool OnDataLoadedCalled { get; private set; }
            public bool OnBeforeSaveCalled { get; private set; }
            public bool MigrateDataCalled { get; private set; }
            public int MigratedFromVersion { get; private set; }

            public TestSaveService(ISaveDataStorage storage) : base(storage)
            {
            }

            protected override void OnDataLoaded(TestSaveData data)
            {
                OnDataLoadedCalled = true;
            }

            protected override void OnBeforeSave(TestSaveData data)
            {
                OnBeforeSaveCalled = true;
            }

            protected override int GetDataVersion(TestSaveData data)
            {
                return data.Version;
            }

            protected override void MigrateData(TestSaveData data, int fromVersion)
            {
                MigrateDataCalled = true;
                MigratedFromVersion = fromVersion;
                data.Version = CurrentVersion;
            }

            // MarkDirtyをテスト用に公開
            public void TestMarkDirty() => MarkDirty();
        }

        #endregion

        #region Setup

        private ISaveDataStorage _mockStorage;
        private TestSaveService _service;

        [SetUp]
        public void Setup()
        {
            _mockStorage = Substitute.For<ISaveDataStorage>();
            _service = new TestSaveService(_mockStorage);
        }

        #endregion

        #region LoadAsync Tests

        [Test]
        public async Task LoadAsync_WhenDataNotExists_CreatesNewData()
        {
            // Arrange
            _mockStorage.LoadAsync<TestSaveData>(TestSaveService.TestSaveKey)
                .Returns(UniTask.FromResult<TestSaveData>(null));

            // Act
            await _service.LoadAsync();

            // Assert
            Assert.That(_service.IsLoaded, Is.True);
            Assert.That(_service.Data, Is.Not.Null);
            Assert.That(_service.IsDirty, Is.False);
            Assert.That(_service.OnDataLoadedCalled, Is.False);
        }

        [Test]
        public async Task LoadAsync_WhenDataExists_LoadsData()
        {
            // Arrange
            var existingData = new TestSaveData { Name = "Test", Score = 100, Version = 1 };
            _mockStorage.LoadAsync<TestSaveData>(TestSaveService.TestSaveKey)
                .Returns(UniTask.FromResult(existingData));

            // Act
            await _service.LoadAsync();

            // Assert
            Assert.That(_service.IsLoaded, Is.True);
            Assert.That(_service.Data.Name, Is.EqualTo("Test"));
            Assert.That(_service.Data.Score, Is.EqualTo(100));
            Assert.That(_service.OnDataLoadedCalled, Is.True);
            Assert.That(_service.IsDirty, Is.False);
        }

        [Test]
        public async Task LoadAsync_WhenVersionOld_MigratesData()
        {
            // Arrange
            var oldData = new TestSaveData { Name = "Old", Score = 50, Version = 1 };
            _mockStorage.LoadAsync<TestSaveData>(TestSaveService.TestSaveKey)
                .Returns(UniTask.FromResult(oldData));
            _service.TestCurrentVersion = 2;

            // Act
            await _service.LoadAsync();

            // Assert
            Assert.That(_service.MigrateDataCalled, Is.True);
            Assert.That(_service.MigratedFromVersion, Is.EqualTo(1));
            // マイグレーション後はダーティフラグがfalse（LoadAsyncの最後でリセット）
            Assert.That(_service.IsDirty, Is.False);
        }

        [Test]
        public async Task LoadAsync_WhenStorageThrows_CreatesNewData()
        {
            // Arrange
            _mockStorage.LoadAsync<TestSaveData>(TestSaveService.TestSaveKey)
                .Returns(UniTask.FromException<TestSaveData>(new Exception("Storage error")));

            // Expect error log
            LogAssert.Expect(LogType.Error, new Regex(@"Failed to load.*Storage error"));

            // Act
            await _service.LoadAsync();

            // Assert
            Assert.That(_service.IsLoaded, Is.True);
            Assert.That(_service.Data, Is.Not.Null);
            Assert.That(_service.IsDirty, Is.False);
        }

        #endregion

        #region SaveAsync Tests

        [Test]
        public async Task SaveAsync_WhenDataLoaded_SavesData()
        {
            // Arrange
            await LoadTestData();
            _service.Data.Score = 200;
            _mockStorage.SaveAsync(TestSaveService.TestSaveKey, Arg.Any<TestSaveData>())
                .Returns(UniTask.CompletedTask);

            // Act
            await _service.SaveAsync();

            // Assert
            await _mockStorage.Received(1).SaveAsync(
                TestSaveService.TestSaveKey,
                Arg.Is<TestSaveData>(d => d.Score == 200));
            Assert.That(_service.OnBeforeSaveCalled, Is.True);
            Assert.That(_service.IsDirty, Is.False);
        }

        [Test]
        public async Task SaveAsync_WhenDataNotLoaded_DoesNotSave()
        {
            // Act
            await _service.SaveAsync();

            // Assert
            await _mockStorage.DidNotReceive().SaveAsync(
                Arg.Any<string>(),
                Arg.Any<TestSaveData>());
        }

        [Test]
        public async Task SaveAsync_WhenStorageThrows_KeepsDirtyFlag()
        {
            // Arrange
            await LoadTestData();
            _service.TestMarkDirty();
            _mockStorage.SaveAsync(TestSaveService.TestSaveKey, Arg.Any<TestSaveData>())
                .Returns(UniTask.FromException(new Exception("Save error")));

            // Expect error log
            LogAssert.Expect(LogType.Error, new Regex(@"Failed to save.*Save error"));

            // Act
            await _service.SaveAsync();

            // Assert
            // 保存失敗時はダーティフラグは変わらない（例外がキャッチされログ出力のみ）
            Assert.That(_service.IsDirty, Is.True);
        }

        #endregion

        #region SaveIfDirtyAsync Tests

        [Test]
        public async Task SaveIfDirtyAsync_WhenDirty_SavesData()
        {
            // Arrange
            await LoadTestData();
            _service.TestMarkDirty();
            _mockStorage.SaveAsync(TestSaveService.TestSaveKey, Arg.Any<TestSaveData>())
                .Returns(UniTask.CompletedTask);

            // Act
            await _service.SaveIfDirtyAsync();

            // Assert
            await _mockStorage.Received(1).SaveAsync(
                TestSaveService.TestSaveKey,
                Arg.Any<TestSaveData>());
        }

        [Test]
        public async Task SaveIfDirtyAsync_WhenNotDirty_DoesNotSave()
        {
            // Arrange
            await LoadTestData();

            // Act
            await _service.SaveIfDirtyAsync();

            // Assert
            await _mockStorage.DidNotReceive().SaveAsync(
                Arg.Any<string>(),
                Arg.Any<TestSaveData>());
        }

        #endregion

        #region DeleteAsync Tests

        [Test]
        public async Task DeleteAsync_DeletesAndCreatesNewData()
        {
            // Arrange
            await LoadTestData();
            _service.Data.Score = 999;
            _mockStorage.DeleteAsync(TestSaveService.TestSaveKey)
                .Returns(UniTask.CompletedTask);

            // Act
            await _service.DeleteAsync();

            // Assert
            await _mockStorage.Received(1).DeleteAsync(TestSaveService.TestSaveKey);
            Assert.That(_service.Data.Score, Is.EqualTo(0)); // 新規データのデフォルト値
            Assert.That(_service.IsDirty, Is.False);
        }

        #endregion

        #region MarkDirty Tests

        [Test]
        public async Task MarkDirty_SetsDirtyFlag()
        {
            // Arrange
            await LoadTestData();
            Assert.That(_service.IsDirty, Is.False);

            // Act
            _service.TestMarkDirty();

            // Assert
            Assert.That(_service.IsDirty, Is.True);
        }

        #endregion

        #region Helper Methods

        private async Task LoadTestData()
        {
            var data = new TestSaveData { Name = "Test", Score = 100, Version = 1 };
            _mockStorage.LoadAsync<TestSaveData>(TestSaveService.TestSaveKey)
                .Returns(UniTask.FromResult(data));
            await _service.LoadAsync();
        }

        #endregion
    }
}
