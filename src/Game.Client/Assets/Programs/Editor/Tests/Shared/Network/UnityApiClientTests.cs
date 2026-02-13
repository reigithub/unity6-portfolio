using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Shared.Services;
using Game.Shared.Services.Network;
using Game.Shared.Services.Network.Cache;
using Game.Shared.Services.Network.Models;
using NSubstitute;
using NUnit.Framework;

namespace Game.Tests.Shared.Network
{
    /// <summary>
    /// UnityApiClientのテスト
    /// HTTP通信部分は統合テストで確認するため、
    /// オフライン/サーキットブレーカー/キャッシュのロジックに焦点を当てる
    /// </summary>
    [TestFixture]
    public class UnityApiClientTests
    {
        private INetworkService _mockNetworkService;
        private IResponseCache _mockCache;
        private UnityApiClient _client;

        [SetUp]
        public void Setup()
        {
            _mockNetworkService = Substitute.For<INetworkService>();
            _mockCache = Substitute.For<IResponseCache>();

            // デフォルトはオンライン、サーキットブレーカーClosed
            _mockNetworkService.IsConnected.Returns(true);
            _mockNetworkService.CanExecute.Returns(true);

            _client = new UnityApiClient(_mockNetworkService, _mockCache);
        }

        #region Test Data

        private class TestResponse
        {
            public int Id;
            public string Name;
        }

        private class TestRequest
        {
            public string Data;
        }

        #endregion

        #region Constructor Tests

        [Test]
        public void Constructor_WithNullNetworkService_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.That(() => new UnityApiClient(null, _mockCache),
                Throws.ArgumentNullException);
        }

        [Test]
        public void Constructor_WithNullCache_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.That(() => new UnityApiClient(_mockNetworkService, null),
                Throws.ArgumentNullException);
        }

        [Test]
        public void Constructor_WithValidDependencies_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => new UnityApiClient(_mockNetworkService, _mockCache));
        }

        #endregion

        #region SetAuthToken / ClearAuthToken Tests

        [Test]
        public void SetAuthToken_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _client.SetAuthToken("test-token"));
        }

        [Test]
        public void SetAuthToken_WithNull_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _client.SetAuthToken(null));
        }

        [Test]
        public void ClearAuthToken_DoesNotThrow()
        {
            // Arrange
            _client.SetAuthToken("test-token");

            // Act & Assert
            Assert.DoesNotThrow(() => _client.ClearAuthToken());
        }

        #endregion

        #region GetAsync - Offline Tests

        [Test]
        public async Task GetAsync_WhenOffline_ReturnsOfflineError()
        {
            // Arrange
            _mockNetworkService.IsConnected.Returns(false);
            _mockCache.GetAsync<TestResponse>(Arg.Any<string>())
                .Returns(UniTask.FromResult<CacheEntry<TestResponse>>(null));

            // Act
            var result = await _client.GetAsync<TestResponse>("api/test");

            // Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.error, Is.EqualTo("Offline"));
        }

        [Test]
        public async Task GetAsync_WhenOfflineWithCache_ReturnsCachedData()
        {
            // Arrange
            _mockNetworkService.IsConnected.Returns(false);

            var cachedData = new TestResponse { Id = 99, Name = "Cached" };
            var cacheEntry = new CacheEntry<TestResponse>(cachedData, TimeSpan.FromMinutes(5));
            _mockCache.GetAsync<TestResponse>(Arg.Any<string>())
                .Returns(UniTask.FromResult(cacheEntry));

            var options = new RequestOptions { FallbackToCache = true };

            // Act
            var result = await _client.GetAsync<TestResponse>("api/test", options);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.FromCache, Is.True);
            Assert.That(result.IsOfflineFallback, Is.True);
            Assert.That(result.Data.Id, Is.EqualTo(99));
        }

        [Test]
        public async Task GetAsync_WhenOfflineWithExpiredCache_ReturnsCachedData()
        {
            // Arrange
            _mockNetworkService.IsConnected.Returns(false);

            var cachedData = new TestResponse { Id = 99, Name = "ExpiredCached" };
            // 期限切れのキャッシュエントリ
            var cacheEntry = new CacheEntry<TestResponse>(cachedData, TimeSpan.FromMilliseconds(-1));
            _mockCache.GetAsync<TestResponse>(Arg.Any<string>())
                .Returns(UniTask.FromResult(cacheEntry));

            var options = new RequestOptions { FallbackToCache = true };

            // Act
            var result = await _client.GetAsync<TestResponse>("api/test", options);

            // Assert - オフライン時は期限切れでも返す
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.FromCache, Is.True);
        }

        [Test]
        public async Task GetAsync_WhenOfflineWithoutFallback_ReturnsOfflineError()
        {
            // Arrange
            _mockNetworkService.IsConnected.Returns(false);

            var options = new RequestOptions { FallbackToCache = false, UseCache = false };

            // Act
            var result = await _client.GetAsync<TestResponse>("api/test", options);

            // Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.error, Is.EqualTo("Offline"));
        }

        #endregion

        #region GetAsync - Circuit Breaker Tests

        [Test]
        public async Task GetAsync_WhenCircuitOpen_ReturnsCircuitOpenError()
        {
            // Arrange
            _mockNetworkService.CanExecute.Returns(false);
            _mockCache.GetAsync<TestResponse>(Arg.Any<string>())
                .Returns(UniTask.FromResult<CacheEntry<TestResponse>>(null));

            // Act
            var result = await _client.GetAsync<TestResponse>("api/test");

            // Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.error, Is.EqualTo("CircuitBreakerOpen"));
            Assert.That(result.StatusCode, Is.EqualTo(503));
        }

        [Test]
        public async Task GetAsync_WhenCircuitOpenWithCache_ReturnsCachedData()
        {
            // Arrange
            _mockNetworkService.CanExecute.Returns(false);

            var cachedData = new TestResponse { Id = 99, Name = "Cached" };
            var cacheEntry = new CacheEntry<TestResponse>(cachedData, TimeSpan.FromMinutes(5));
            _mockCache.GetAsync<TestResponse>(Arg.Any<string>())
                .Returns(UniTask.FromResult(cacheEntry));

            var options = new RequestOptions { FallbackToCache = true };

            // Act
            var result = await _client.GetAsync<TestResponse>("api/test", options);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.FromCache, Is.True);
            Assert.That(result.IsCircuitOpenFallback, Is.True);
        }

        #endregion

        #region GetAsync - Cache Tests

        [Test]
        public async Task GetAsync_WhenCacheHit_ReturnsCachedDataWithoutNetworkCall()
        {
            // Arrange
            var cachedData = new TestResponse { Id = 99, Name = "Cached" };
            var cacheEntry = new CacheEntry<TestResponse>(cachedData, TimeSpan.FromMinutes(5));
            _mockCache.GetAsync<TestResponse>("prefix_api/test")
                .Returns(UniTask.FromResult(cacheEntry));

            var options = new RequestOptions
            {
                UseCache = true,
                CacheKeyPrefix = "prefix_"
            };

            // Act
            var result = await _client.GetAsync<TestResponse>("api/test", options);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.FromCache, Is.True);
            Assert.That(result.Data.Id, Is.EqualTo(99));

            // NetworkServiceのRecordSuccess/RecordFailureは呼ばれない
            _mockNetworkService.DidNotReceive().RecordSuccess();
            _mockNetworkService.DidNotReceive().RecordFailure();
        }

        [Test]
        public async Task GetAsync_WhenCacheExpired_DoesNotReturnExpiredCache()
        {
            // Arrange
            var cachedData = new TestResponse { Id = 99, Name = "ExpiredCached" };
            // 期限切れのキャッシュエントリ
            var cacheEntry = new CacheEntry<TestResponse>(cachedData, TimeSpan.FromMilliseconds(-1));
            _mockCache.GetAsync<TestResponse>(Arg.Any<string>())
                .Returns(UniTask.FromResult(cacheEntry));

            var options = new RequestOptions { UseCache = true };

            // Act
            // オンラインだが期限切れキャッシュの場合、HTTP通信を試みる
            // この場合、UnityWebRequestが失敗するため、テストは通信部分まで進む
            // 純粋なユニットテストでは、キャッシュが返されないことを確認
            var result = await _client.GetAsync<TestResponse>("api/test", options);

            // Assert - オンライン時は期限切れキャッシュを返さず、HTTP通信を試みる
            // HTTP通信はモックできないため、この場合は失敗する
            Assert.That(result.FromCache, Is.False);
        }

        [Test]
        public async Task GetAsync_CacheKeyIncludesPrefix()
        {
            // Arrange
            _mockNetworkService.IsConnected.Returns(false);
            _mockCache.GetAsync<TestResponse>(Arg.Any<string>())
                .Returns(UniTask.FromResult<CacheEntry<TestResponse>>(null));

            var options = new RequestOptions
            {
                UseCache = true,
                CacheKeyPrefix = "ranking_"
            };

            // Act
            await _client.GetAsync<TestResponse>("api/test", options);

            // Assert - キャッシュキーにプレフィックスが含まれる
            await _mockCache.Received(1).GetAsync<TestResponse>("ranking_api/test");
        }

        #endregion

        #region PostAsync - Offline Tests

        [Test]
        public async Task PostAsync_WhenOffline_ReturnsOfflineError()
        {
            // Arrange
            _mockNetworkService.IsConnected.Returns(false);
            var request = new TestRequest { Data = "test" };

            // Act
            var result = await _client.PostAsync<TestRequest, TestResponse>("api/test", request);

            // Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.error, Is.EqualTo("Offline"));
        }

        #endregion

        #region PostAsync - Circuit Breaker Tests

        [Test]
        public async Task PostAsync_WhenCircuitOpen_ReturnsCircuitOpenError()
        {
            // Arrange
            _mockNetworkService.CanExecute.Returns(false);
            var request = new TestRequest { Data = "test" };

            // Act
            var result = await _client.PostAsync<TestRequest, TestResponse>("api/test", request);

            // Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.error, Is.EqualTo("CircuitBreakerOpen"));
            Assert.That(result.StatusCode, Is.EqualTo(503));
        }

        #endregion

        #region DeleteAsync - Offline Tests

        [Test]
        public async Task DeleteAsync_WhenOffline_ReturnsOfflineError()
        {
            // Arrange
            _mockNetworkService.IsConnected.Returns(false);

            // Act
            var result = await _client.DeleteAsync<TestResponse>("api/test/1");

            // Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.error, Is.EqualTo("Offline"));
        }

        #endregion

        #region DeleteAsync - Circuit Breaker Tests

        [Test]
        public async Task DeleteAsync_WhenCircuitOpen_ReturnsCircuitOpenError()
        {
            // Arrange
            _mockNetworkService.CanExecute.Returns(false);

            // Act
            var result = await _client.DeleteAsync<TestResponse>("api/test/1");

            // Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.error, Is.EqualTo("CircuitBreakerOpen"));
            Assert.That(result.StatusCode, Is.EqualTo(503));
        }

        #endregion

        #region ApiResponse Static Methods Tests

        [Test]
        public void ApiResponse_OfflineError_CreatesCorrectResponse()
        {
            // Act
            var response = ApiResponse<TestResponse>.OfflineError();

            // Assert
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.Error.error, Is.EqualTo("Offline"));
            Assert.That(response.Error.IsOfflineError, Is.True);
            Assert.That(response.StatusCode, Is.EqualTo(0));
        }

        [Test]
        public void ApiResponse_CircuitOpenError_CreatesCorrectResponse()
        {
            // Arrange
            var remainingTime = TimeSpan.FromSeconds(30);

            // Act
            var response = ApiResponse<TestResponse>.CircuitOpenError(remainingTime);

            // Assert
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.Error.error, Is.EqualTo("CircuitBreakerOpen"));
            Assert.That(response.StatusCode, Is.EqualTo(503));
            Assert.That(response.Error.message, Does.Contain("30"));
        }

        [Test]
        public void ApiResponse_SuccessFromCache_CreatesCorrectResponse()
        {
            // Arrange
            var data = new TestResponse { Id = 1, Name = "Test" };

            // Act
            var response = ApiResponse<TestResponse>.SuccessFromCache(data);

            // Assert
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.FromCache, Is.True);
            Assert.That(response.Data.Id, Is.EqualTo(1));
            Assert.That(response.StatusCode, Is.EqualTo(200));
        }

        [Test]
        public void ApiResponse_SuccessFromCache_WithOfflineFlag_SetsCorrectFlags()
        {
            // Arrange
            var data = new TestResponse { Id = 1, Name = "Test" };

            // Act
            var response = ApiResponse<TestResponse>.SuccessFromCache(data, isOffline: true);

            // Assert
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.FromCache, Is.True);
            Assert.That(response.IsOfflineFallback, Is.True);
            Assert.That(response.IsCircuitOpenFallback, Is.False);
        }

        [Test]
        public void ApiResponse_SuccessFromCache_WithCircuitOpenFlag_SetsCorrectFlags()
        {
            // Arrange
            var data = new TestResponse { Id = 1, Name = "Test" };

            // Act
            var response = ApiResponse<TestResponse>.SuccessFromCache(data, isCircuitOpen: true);

            // Assert
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.FromCache, Is.True);
            Assert.That(response.IsOfflineFallback, Is.False);
            Assert.That(response.IsCircuitOpenFallback, Is.True);
        }

        #endregion
    }
}
