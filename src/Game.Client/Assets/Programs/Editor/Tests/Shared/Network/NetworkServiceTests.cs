using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Shared.Services;
using Game.Shared.Services.Network;
using Game.Shared.Services.Network.Cache;
using Game.Shared.Services.Network.Connectivity;
using Game.Shared.Services.Network.Models;
using NSubstitute;
using NUnit.Framework;
using R3;

namespace Game.Tests.Shared.Network
{
    [TestFixture]
    public class NetworkServiceTests
    {
        private IApiClient _mockApiClient;
        private IConnectivityChecker _mockConnectivityChecker;
        private IResponseCache _mockCache;
        private NetworkService _service;
        private ReactiveProperty<bool> _connectivityProperty;

        [SetUp]
        public void Setup()
        {
            _mockApiClient = Substitute.For<IApiClient>();
            _mockConnectivityChecker = Substitute.For<IConnectivityChecker>();
            _mockCache = Substitute.For<IResponseCache>();

            _connectivityProperty = new ReactiveProperty<bool>(true);
            _mockConnectivityChecker.IsConnected.Returns(true);
            _mockConnectivityChecker.OnConnectivityChanged.Returns(_connectivityProperty.DistinctUntilChanged());

            _service = new NetworkService(_mockApiClient, _mockConnectivityChecker, _mockCache);
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            _connectivityProperty?.Dispose();
        }

        #region Test Data

        private class TestResponse
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        private class TestRequest
        {
            public string Data { get; set; }
        }

        #endregion

        #region Constructor Tests

        [Test]
        public void Constructor_StartsMonitoring()
        {
            // Assert
            _mockConnectivityChecker.Received(1).StartMonitoring();
        }

        [Test]
        public void Constructor_WithNullApiClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.That(() => new NetworkService(null, _mockConnectivityChecker, _mockCache),
                Throws.ArgumentNullException);
        }

        [Test]
        public void Constructor_WithNullConnectivityChecker_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.That(() => new NetworkService(_mockApiClient, null, _mockCache),
                Throws.ArgumentNullException);
        }

        [Test]
        public void Constructor_WithNullCache_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.That(() => new NetworkService(_mockApiClient, _mockConnectivityChecker, null),
                Throws.ArgumentNullException);
        }

        #endregion

        #region IsConnected Tests

        [Test]
        public void IsConnected_ReturnsConnectivityCheckerValue()
        {
            // Arrange
            _mockConnectivityChecker.IsConnected.Returns(false);

            // Act & Assert
            Assert.That(_service.IsConnected, Is.False);
        }

        #endregion

        #region GetAsync Tests

        [Test]
        public async Task GetAsync_WhenOnline_CallsApiClient()
        {
            // Arrange
            var response = new ApiResponse<TestResponse>
            {
                IsSuccess = true,
                Data = new TestResponse { Id = 1, Name = "Test" },
                StatusCode = 200
            };
            _mockApiClient.GetAsync<TestResponse>("api/test", Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(response));

            // Act
            var result = await _service.GetAsync<TestResponse>("api/test");

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Data.Id, Is.EqualTo(1));
            Assert.That(result.Data.Name, Is.EqualTo("Test"));
            Assert.That(result.FromCache, Is.False);
        }

        [Test]
        public async Task GetAsync_WhenOffline_ReturnsCachedData()
        {
            // Arrange
            _mockConnectivityChecker.IsConnected.Returns(false);
            var cachedData = new TestResponse { Id = 99, Name = "Cached" };
            var cacheEntry = new CacheEntry<TestResponse>(cachedData, TimeSpan.FromMinutes(5));
            _mockCache.GetAsync<TestResponse>("api/test")
                .Returns(UniTask.FromResult(cacheEntry));

            // Act
            var result = await _service.GetAsync<TestResponse>("api/test",
                RequestOptions.WithCache(TimeSpan.FromMinutes(5)));

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Data.Id, Is.EqualTo(99));
            Assert.That(result.FromCache, Is.True);
            Assert.That(result.IsOffline, Is.True);
        }

        [Test]
        public async Task GetAsync_WhenOfflineAndNoCache_ReturnsOfflineError()
        {
            // Arrange
            _mockConnectivityChecker.IsConnected.Returns(false);
            _mockCache.GetAsync<TestResponse>(Arg.Any<string>())
                .Returns(UniTask.FromResult<CacheEntry<TestResponse>>(null));

            // Act
            var result = await _service.GetAsync<TestResponse>("api/test");

            // Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.IsOffline, Is.True);
            Assert.That(result.Error.Type, Is.EqualTo(NetworkErrorType.ConnectionError));
        }

        [Test]
        public async Task GetAsync_WithCacheOption_SavesSuccessfulResponse()
        {
            // Arrange
            var response = new ApiResponse<TestResponse>
            {
                IsSuccess = true,
                Data = new TestResponse { Id = 1, Name = "Test" },
                StatusCode = 200
            };
            _mockApiClient.GetAsync<TestResponse>("api/test", Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(response));
            _mockCache.GetAsync<TestResponse>(Arg.Any<string>())
                .Returns(UniTask.FromResult<CacheEntry<TestResponse>>(null));

            var options = RequestOptions.WithCache(TimeSpan.FromMinutes(5));

            // Act
            await _service.GetAsync<TestResponse>("api/test", options);

            // Assert
            await _mockCache.Received(1).SetAsync(
                "api/test",
                Arg.Is<TestResponse>(r => r.Id == 1),
                TimeSpan.FromMinutes(5));
        }

        [Test]
        public async Task GetAsync_WithCacheHit_DoesNotCallApiClient()
        {
            // Arrange
            var cachedData = new TestResponse { Id = 99, Name = "Cached" };
            var cacheEntry = new CacheEntry<TestResponse>(cachedData, TimeSpan.FromMinutes(5));
            _mockCache.GetAsync<TestResponse>("api/test")
                .Returns(UniTask.FromResult(cacheEntry));

            var options = RequestOptions.WithCache(TimeSpan.FromMinutes(5));

            // Act
            var result = await _service.GetAsync<TestResponse>("api/test", options);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.FromCache, Is.True);
            await _mockApiClient.DidNotReceive().GetAsync<TestResponse>(
                Arg.Any<string>(),
                Arg.Any<RequestOptions>(),
                Arg.Any<CancellationToken>());
        }

        #endregion

        #region PostAsync Tests

        [Test]
        public async Task PostAsync_WhenOnline_CallsApiClient()
        {
            // Arrange
            var request = new TestRequest { Data = "test" };
            var response = new ApiResponse<TestResponse>
            {
                IsSuccess = true,
                Data = new TestResponse { Id = 1 },
                StatusCode = 201
            };
            _mockApiClient.PostAsync<TestRequest, TestResponse>("api/test", request, Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(response));

            // Act
            var result = await _service.PostAsync<TestRequest, TestResponse>("api/test", request);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.StatusCode, Is.EqualTo(201));
        }

        [Test]
        public async Task PostAsync_WhenOffline_ReturnsOfflineError()
        {
            // Arrange
            _mockConnectivityChecker.IsConnected.Returns(false);
            var request = new TestRequest { Data = "test" };

            // Act
            var result = await _service.PostAsync<TestRequest, TestResponse>("api/test", request);

            // Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.IsOffline, Is.True);
        }

        #endregion

        #region DeleteAsync Tests

        [Test]
        public async Task DeleteAsync_WhenOnline_CallsApiClient()
        {
            // Arrange
            var response = new ApiResponse<TestResponse>
            {
                IsSuccess = true,
                Data = new TestResponse { Id = 1 },
                StatusCode = 200
            };
            _mockApiClient.DeleteAsync<TestResponse>("api/test/1", Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(response));

            // Act
            var result = await _service.DeleteAsync<TestResponse>("api/test/1");

            // Assert
            Assert.That(result.IsSuccess, Is.True);
        }

        [Test]
        public async Task DeleteAsync_WhenOffline_ReturnsOfflineError()
        {
            // Arrange
            _mockConnectivityChecker.IsConnected.Returns(false);

            // Act
            var result = await _service.DeleteAsync<TestResponse>("api/test/1");

            // Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.IsOffline, Is.True);
        }

        #endregion

        #region Error Handling Tests

        [Test]
        public async Task GetAsync_WhenApiReturns401_ReturnsAuthenticationError()
        {
            // Arrange
            var response = new ApiResponse<TestResponse>
            {
                IsSuccess = false,
                Error = new ApiErrorResponse { error = "Unauthorized", message = "認証が必要です" },
                StatusCode = 401
            };
            _mockApiClient.GetAsync<TestResponse>("api/test", Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(response));

            // Act
            var result = await _service.GetAsync<TestResponse>("api/test");

            // Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Type, Is.EqualTo(NetworkErrorType.AuthenticationError));
        }

        [Test]
        public async Task GetAsync_WhenApiReturns500_ReturnsServerError()
        {
            // Arrange
            var response = new ApiResponse<TestResponse>
            {
                IsSuccess = false,
                Error = new ApiErrorResponse { error = "InternalError", message = "サーバーエラー" },
                StatusCode = 500
            };
            _mockApiClient.GetAsync<TestResponse>("api/test", Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(response));

            // Act
            var result = await _service.GetAsync<TestResponse>("api/test");

            // Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Type, Is.EqualTo(NetworkErrorType.ServerError));
        }

        [Test]
        public async Task GetAsync_WhenApiReturns429_ReturnsRateLimitedError()
        {
            // Arrange
            var response = new ApiResponse<TestResponse>
            {
                IsSuccess = false,
                Error = new ApiErrorResponse { error = "TooManyRequests", message = "リクエスト制限" },
                StatusCode = 429
            };
            _mockApiClient.GetAsync<TestResponse>("api/test", Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(response));

            // Act
            var result = await _service.GetAsync<TestResponse>("api/test");

            // Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Type, Is.EqualTo(NetworkErrorType.RateLimited));
        }

        #endregion

        #region Auth Token Tests

        [Test]
        public void SetAuthToken_CallsApiClient()
        {
            // Act
            _service.SetAuthToken("test-token");

            // Assert
            _mockApiClient.Received(1).SetAuthToken("test-token");
        }

        [Test]
        public void ClearAuthToken_CallsApiClient()
        {
            // Act
            _service.ClearAuthToken();

            // Assert
            _mockApiClient.Received(1).ClearAuthToken();
        }

        #endregion

        #region ClearCacheAsync Tests

        [Test]
        public async Task ClearCacheAsync_CallsCache()
        {
            // Act
            await _service.ClearCacheAsync();

            // Assert
            await _mockCache.Received(1).ClearAsync();
        }

        #endregion

        #region Dispose Tests

        [Test]
        public void Dispose_StopsMonitoring()
        {
            // Act
            _service.Dispose();

            // Assert
            _mockConnectivityChecker.Received(1).StopMonitoring();
        }

        #endregion
    }
}
