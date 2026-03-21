using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Game.Shared.Services.Network.Cache;

namespace Game.Tests.Shared.Network
{
    [TestFixture]
    public class MemoryResponseCacheTests
    {
        private MemoryResponseCache _cache;

        [SetUp]
        public void Setup()
        {
            _cache = new MemoryResponseCache();
        }

        #region Test Data

        private class TestData
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        #endregion

        #region SetAsync and GetAsync Tests

        [Test]
        public async Task SetAsync_AndGetAsync_ReturnsStoredData()
        {
            // Arrange
            var data = new TestData { Id = 1, Name = "Test" };

            // Act
            await _cache.SetAsync("key1", data);
            var result = await _cache.GetAsync<TestData>("key1");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Data.Id, Is.EqualTo(1));
            Assert.That(result.Data.Name, Is.EqualTo("Test"));
        }

        [Test]
        public async Task GetAsync_WhenKeyNotExists_ReturnsNull()
        {
            // Act
            var result = await _cache.GetAsync<TestData>("nonexistent");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task GetAsync_WhenKeyIsNull_ReturnsNull()
        {
            // Act
            var result = await _cache.GetAsync<TestData>(null);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task GetAsync_WhenKeyIsEmpty_ReturnsNull()
        {
            // Act
            var result = await _cache.GetAsync<TestData>("");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task SetAsync_WithNullKey_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrowAsync(async () =>
                await _cache.SetAsync<TestData>(null, new TestData()));
        }

        [Test]
        public async Task SetAsync_OverwritesExistingKey()
        {
            // Arrange
            var data1 = new TestData { Id = 1, Name = "First" };
            var data2 = new TestData { Id = 2, Name = "Second" };

            // Act
            await _cache.SetAsync("key1", data1);
            await _cache.SetAsync("key1", data2);
            var result = await _cache.GetAsync<TestData>("key1");

            // Assert
            Assert.That(result.Data.Id, Is.EqualTo(2));
            Assert.That(result.Data.Name, Is.EqualTo("Second"));
        }

        #endregion

        #region Expiration Tests

        [Test]
        public async Task GetAsync_WhenExpired_ReturnsNull()
        {
            // Arrange
            var data = new TestData { Id = 1, Name = "Test" };
            await _cache.SetAsync("key1", data, TimeSpan.FromMilliseconds(50));

            // Act
            await Task.Delay(100); // 期限切れを待つ
            var result = await _cache.GetAsync<TestData>("key1");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task GetAsync_WhenNotExpired_ReturnsData()
        {
            // Arrange
            var data = new TestData { Id = 1, Name = "Test" };
            await _cache.SetAsync("key1", data, TimeSpan.FromSeconds(10));

            // Act
            var result = await _cache.GetAsync<TestData>("key1");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsExpired, Is.False);
        }

        [Test]
        public async Task SetAsync_WithNoDuration_DoesNotExpire()
        {
            // Arrange
            var data = new TestData { Id = 1, Name = "Test" };

            // Act
            await _cache.SetAsync("key1", data); // 期限なし
            var result = await _cache.GetAsync<TestData>("key1");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.ExpiresAt, Is.EqualTo(DateTime.MaxValue));
        }

        #endregion

        #region RemoveAsync Tests

        [Test]
        public async Task RemoveAsync_RemovesEntry()
        {
            // Arrange
            await _cache.SetAsync("key1", new TestData { Id = 1 });

            // Act
            await _cache.RemoveAsync("key1");
            var result = await _cache.GetAsync<TestData>("key1");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task RemoveAsync_NonexistentKey_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrowAsync(async () =>
                await _cache.RemoveAsync("nonexistent"));
        }

        #endregion

        #region ClearAsync Tests

        [Test]
        public async Task ClearAsync_RemovesAllEntries()
        {
            // Arrange
            await _cache.SetAsync("key1", new TestData { Id = 1 });
            await _cache.SetAsync("key2", new TestData { Id = 2 });
            await _cache.SetAsync("key3", new TestData { Id = 3 });

            // Act
            await _cache.ClearAsync();

            // Assert
            Assert.That(_cache.Count, Is.EqualTo(0));
            Assert.That(await _cache.GetAsync<TestData>("key1"), Is.Null);
            Assert.That(await _cache.GetAsync<TestData>("key2"), Is.Null);
            Assert.That(await _cache.GetAsync<TestData>("key3"), Is.Null);
        }

        #endregion

        #region CleanupExpiredAsync Tests

        [Test]
        public async Task CleanupExpiredAsync_RemovesOnlyExpiredEntries()
        {
            // Arrange
            await _cache.SetAsync("expired1", new TestData { Id = 1 }, TimeSpan.FromMilliseconds(50));
            await _cache.SetAsync("expired2", new TestData { Id = 2 }, TimeSpan.FromMilliseconds(50));
            await _cache.SetAsync("valid", new TestData { Id = 3 }, TimeSpan.FromSeconds(10));

            await Task.Delay(100); // 期限切れを待つ

            // Act
            await _cache.CleanupExpiredAsync();

            // Assert
            Assert.That(await _cache.GetAsync<TestData>("expired1"), Is.Null);
            Assert.That(await _cache.GetAsync<TestData>("expired2"), Is.Null);
            Assert.That(await _cache.GetAsync<TestData>("valid"), Is.Not.Null);
        }

        #endregion

        #region Contains Tests

        [Test]
        public async Task Contains_WhenKeyExists_ReturnsTrue()
        {
            // Arrange
            await _cache.SetAsync("key1", new TestData { Id = 1 });

            // Act & Assert
            Assert.That(_cache.Contains("key1"), Is.True);
        }

        [Test]
        public void Contains_WhenKeyNotExists_ReturnsFalse()
        {
            // Act & Assert
            Assert.That(_cache.Contains("nonexistent"), Is.False);
        }

        [Test]
        public async Task Contains_WhenExpired_ReturnsFalse()
        {
            // Arrange
            await _cache.SetAsync("key1", new TestData { Id = 1 }, TimeSpan.FromMilliseconds(50));
            await Task.Delay(100);

            // Act & Assert
            Assert.That(_cache.Contains("key1"), Is.False);
        }

        [Test]
        public void Contains_WhenKeyIsNull_ReturnsFalse()
        {
            // Act & Assert
            Assert.That(_cache.Contains(null), Is.False);
        }

        #endregion

        #region Count Tests

        [Test]
        public async Task Count_ReturnsCorrectNumber()
        {
            // Arrange
            await _cache.SetAsync("key1", new TestData { Id = 1 });
            await _cache.SetAsync("key2", new TestData { Id = 2 });

            // Assert
            Assert.That(_cache.Count, Is.EqualTo(2));
        }

        [Test]
        public void Count_WhenEmpty_ReturnsZero()
        {
            // Assert
            Assert.That(_cache.Count, Is.EqualTo(0));
        }

        #endregion

        #region Max Entries Tests

        [Test]
        public async Task SetAsync_WhenMaxEntriesReached_RemovesOldestEntries()
        {
            // Arrange
            var smallCache = new MemoryResponseCache(5);

            // Act - 10エントリを追加
            for (int i = 0; i < 10; i++)
            {
                await smallCache.SetAsync($"key{i}", new TestData { Id = i });
                await Task.Delay(10); // 順序を保証
            }

            // Assert - 最大数を超えないことを確認
            Assert.That(smallCache.Count, Is.LessThanOrEqualTo(5));
        }

        #endregion

        #region Type Mismatch Tests

        [Test]
        public async Task GetAsync_WithWrongType_ReturnsNull()
        {
            // Arrange
            await _cache.SetAsync("key1", new TestData { Id = 1, Name = "Test" });

            // Act - 異なる型で取得を試みる
            var result = await _cache.GetAsync<string>("key1");

            // Assert
            Assert.That(result, Is.Null);
        }

        #endregion

        #region CacheEntry Tests

        [Test]
        public async Task CacheEntry_HasCorrectMetadata()
        {
            // Arrange
            var duration = TimeSpan.FromMinutes(5);
            var beforeSet = DateTime.UtcNow.AddMilliseconds(-500);
            await _cache.SetAsync("key1", new TestData { Id = 1 }, duration);
            var afterSet = DateTime.UtcNow.AddMilliseconds(500);

            // Act
            var result = await _cache.GetAsync<TestData>("key1");

            // Assert
            Assert.That(result.CreatedAt, Is.GreaterThanOrEqualTo(beforeSet));
            Assert.That(result.CreatedAt, Is.LessThanOrEqualTo(afterSet));
            Assert.That(result.ExpiresAt, Is.GreaterThan(result.CreatedAt));
            Assert.That(result.TimeToLive.TotalMinutes, Is.GreaterThan(4).And.LessThanOrEqualTo(5));
        }

        #endregion
    }
}
