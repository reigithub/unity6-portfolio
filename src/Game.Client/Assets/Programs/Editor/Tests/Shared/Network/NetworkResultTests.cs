using NUnit.Framework;
using Game.Shared.Services.Network.Models;

namespace Game.Tests.Shared.Network
{
    [TestFixture]
    public class NetworkResultTests
    {
        #region Test Data

        private class TestData
        {
            public int Value { get; set; }
        }

        #endregion

        #region Success Tests

        [Test]
        public void Success_CreatesSuccessResult()
        {
            // Act
            var result = NetworkResult<TestData>.Success(new TestData { Value = 42 });

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Data.Value, Is.EqualTo(42));
            Assert.That(result.Error, Is.Null);
            Assert.That(result.FromCache, Is.False);
            Assert.That(result.IsOffline, Is.False);
        }

        [Test]
        public void Success_WithStatusCode_SetsStatusCode()
        {
            // Act
            var result = NetworkResult<TestData>.Success(new TestData(), 201);

            // Assert
            Assert.That(result.StatusCode, Is.EqualTo(201));
        }

        [Test]
        public void Success_WithFromCache_SetsFromCache()
        {
            // Act
            var result = NetworkResult<TestData>.Success(new TestData(), fromCache: true);

            // Assert
            Assert.That(result.FromCache, Is.True);
        }

        #endregion

        #region FromCacheSuccess Tests

        [Test]
        public void FromCacheSuccess_CreatesSuccessResultFromCache()
        {
            // Act
            var result = NetworkResult<TestData>.FromCacheSuccess(new TestData { Value = 99 });

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Data.Value, Is.EqualTo(99));
            Assert.That(result.FromCache, Is.True);
            Assert.That(result.IsOffline, Is.False);
        }

        [Test]
        public void FromCacheSuccess_WithIsOffline_SetsIsOffline()
        {
            // Act
            var result = NetworkResult<TestData>.FromCacheSuccess(new TestData(), isOffline: true);

            // Assert
            Assert.That(result.FromCache, Is.True);
            Assert.That(result.IsOffline, Is.True);
        }

        #endregion

        #region Failure Tests

        [Test]
        public void Failure_CreatesFailureResult()
        {
            // Arrange
            var error = NetworkError.ConnectionFailed("Test error");

            // Act
            var result = NetworkResult<TestData>.Failure(error);

            // Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Data, Is.Null);
            Assert.That(result.Error, Is.SameAs(error));
        }

        [Test]
        public void Failure_WithStatusCode_SetsStatusCode()
        {
            // Arrange
            var error = NetworkError.ServerFailed(500);

            // Act
            var result = NetworkResult<TestData>.Failure(error, 500);

            // Assert
            Assert.That(result.StatusCode, Is.EqualTo(500));
        }

        [Test]
        public void Failure_WithOfflineError_SetsIsOffline()
        {
            // Arrange
            var error = NetworkError.ConnectionFailed();

            // Act
            var result = NetworkResult<TestData>.Failure(error);

            // Assert
            Assert.That(result.IsOffline, Is.True);
        }

        #endregion

        #region Offline Tests

        [Test]
        public void Offline_CreatesOfflineResult()
        {
            // Act
            var result = NetworkResult<TestData>.Offline();

            // Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.IsOffline, Is.True);
            Assert.That(result.Error.Type, Is.EqualTo(NetworkErrorType.ConnectionError));
        }

        [Test]
        public void Offline_WithMessage_SetsErrorMessage()
        {
            // Act
            var result = NetworkResult<TestData>.Offline("Custom offline message");

            // Assert
            Assert.That(result.Error.Message, Is.EqualTo("Custom offline message"));
        }

        #endregion

        #region Map Tests

        [Test]
        public void Map_WhenSuccess_TransformsData()
        {
            // Arrange
            var original = NetworkResult<TestData>.Success(new TestData { Value = 42 });

            // Act
            var mapped = original.Map(d => d.Value.ToString());

            // Assert
            Assert.That(mapped.IsSuccess, Is.True);
            Assert.That(mapped.Data, Is.EqualTo("42"));
        }

        [Test]
        public void Map_WhenFailure_PreservesError()
        {
            // Arrange
            var error = NetworkError.ServerFailed(500);
            var original = NetworkResult<TestData>.Failure(error, 500);

            // Act
            var mapped = original.Map(d => d.Value.ToString());

            // Assert
            Assert.That(mapped.IsSuccess, Is.False);
            Assert.That(mapped.Error, Is.SameAs(error));
            Assert.That(mapped.StatusCode, Is.EqualTo(500));
        }

        [Test]
        public void Map_PreservesFromCacheFlag()
        {
            // Arrange
            var original = NetworkResult<TestData>.FromCacheSuccess(new TestData { Value = 1 });

            // Act
            var mapped = original.Map(d => d.Value * 2);

            // Assert
            Assert.That(mapped.FromCache, Is.True);
        }

        #endregion

        #region OnSuccess Tests

        [Test]
        public void OnSuccess_WhenSuccess_ExecutesAction()
        {
            // Arrange
            var result = NetworkResult<TestData>.Success(new TestData { Value = 42 });
            var actionExecuted = false;
            var receivedValue = 0;

            // Act
            result.OnSuccess(d =>
            {
                actionExecuted = true;
                receivedValue = d.Value;
            });

            // Assert
            Assert.That(actionExecuted, Is.True);
            Assert.That(receivedValue, Is.EqualTo(42));
        }

        [Test]
        public void OnSuccess_WhenFailure_DoesNotExecuteAction()
        {
            // Arrange
            var result = NetworkResult<TestData>.Failure(NetworkError.ConnectionFailed());
            var actionExecuted = false;

            // Act
            result.OnSuccess(_ => actionExecuted = true);

            // Assert
            Assert.That(actionExecuted, Is.False);
        }

        [Test]
        public void OnSuccess_ReturnsSameResult()
        {
            // Arrange
            var original = NetworkResult<TestData>.Success(new TestData { Value = 42 });

            // Act
            var returned = original.OnSuccess(_ => { });

            // Assert
            Assert.That(returned.Data.Value, Is.EqualTo(original.Data.Value));
        }

        #endregion

        #region OnFailure Tests

        [Test]
        public void OnFailure_WhenFailure_ExecutesAction()
        {
            // Arrange
            var error = NetworkError.ServerFailed(500);
            var result = NetworkResult<TestData>.Failure(error);
            var actionExecuted = false;
            NetworkError receivedError = null;

            // Act
            result.OnFailure(e =>
            {
                actionExecuted = true;
                receivedError = e;
            });

            // Assert
            Assert.That(actionExecuted, Is.True);
            Assert.That(receivedError, Is.SameAs(error));
        }

        [Test]
        public void OnFailure_WhenSuccess_DoesNotExecuteAction()
        {
            // Arrange
            var result = NetworkResult<TestData>.Success(new TestData());
            var actionExecuted = false;

            // Act
            result.OnFailure(_ => actionExecuted = true);

            // Assert
            Assert.That(actionExecuted, Is.False);
        }

        #endregion

        #region ToString Tests

        [Test]
        public void ToString_WhenSuccess_ContainsSuccessInfo()
        {
            // Arrange
            var result = NetworkResult<string>.Success("test data");

            // Act
            var str = result.ToString();

            // Assert
            Assert.That(str, Does.Contain("Success"));
        }

        [Test]
        public void ToString_WhenFromCache_ContainsCacheInfo()
        {
            // Arrange
            var result = NetworkResult<string>.FromCacheSuccess("cached data");

            // Act
            var str = result.ToString();

            // Assert
            Assert.That(str, Does.Contain("cache"));
        }

        [Test]
        public void ToString_WhenFailure_ContainsFailureInfo()
        {
            // Arrange
            var result = NetworkResult<string>.Failure(NetworkError.ServerFailed(500));

            // Act
            var str = result.ToString();

            // Assert
            Assert.That(str, Does.Contain("Failure"));
        }

        #endregion
    }
}
