using System.Collections.Generic;
using NUnit.Framework;
using Game.Shared.Services.Network.Policies;

namespace Game.Tests.Shared.Network
{
    [TestFixture]
    public class RetryPolicyTests
    {
        #region GetDelayMs Tests

        [Test]
        public void GetDelayMs_FirstRetry_ReturnsInitialDelay()
        {
            // Arrange
            var policy = new RetryPolicy
            {
                InitialDelayMs = 1000,
                BackoffMultiplier = 2.0
            };

            // Act
            var delay = policy.GetDelayMs(0);

            // Assert
            Assert.That(delay, Is.EqualTo(1000));
        }

        [Test]
        public void GetDelayMs_SecondRetry_ReturnsDoubledDelay()
        {
            // Arrange
            var policy = new RetryPolicy
            {
                InitialDelayMs = 1000,
                BackoffMultiplier = 2.0
            };

            // Act
            var delay = policy.GetDelayMs(1);

            // Assert
            Assert.That(delay, Is.EqualTo(2000));
        }

        [Test]
        public void GetDelayMs_ThirdRetry_ReturnsQuadrupledDelay()
        {
            // Arrange
            var policy = new RetryPolicy
            {
                InitialDelayMs = 1000,
                BackoffMultiplier = 2.0
            };

            // Act
            var delay = policy.GetDelayMs(2);

            // Assert
            Assert.That(delay, Is.EqualTo(4000));
        }

        [Test]
        public void GetDelayMs_ExceedsMaxDelay_ReturnsMaxDelay()
        {
            // Arrange
            var policy = new RetryPolicy
            {
                InitialDelayMs = 1000,
                MaxDelayMs = 5000,
                BackoffMultiplier = 2.0
            };

            // Act
            var delay = policy.GetDelayMs(10); // 1000 * 2^10 = 1024000 > 5000

            // Assert
            Assert.That(delay, Is.EqualTo(5000));
        }

        [Test]
        public void GetDelayMs_NegativeAttempt_ThrowsException()
        {
            // Arrange
            var policy = RetryPolicy.Default;

            // Act & Assert
            Assert.That(() => policy.GetDelayMs(-1),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void GetDelayMs_CustomMultiplier_CalculatesCorrectly()
        {
            // Arrange
            var policy = new RetryPolicy
            {
                InitialDelayMs = 500,
                BackoffMultiplier = 1.5,
                MaxDelayMs = 10000
            };

            // Act & Assert
            Assert.That(policy.GetDelayMs(0), Is.EqualTo(500));  // 500 * 1.5^0 = 500
            Assert.That(policy.GetDelayMs(1), Is.EqualTo(750));  // 500 * 1.5^1 = 750
            Assert.That(policy.GetDelayMs(2), Is.EqualTo(1125)); // 500 * 1.5^2 = 1125
        }

        #endregion

        #region IsRetryableStatusCode Tests

        [Test]
        [TestCase(408, true, Description = "Request Timeout")]
        [TestCase(429, true, Description = "Too Many Requests")]
        [TestCase(500, true, Description = "Internal Server Error")]
        [TestCase(502, true, Description = "Bad Gateway")]
        [TestCase(503, true, Description = "Service Unavailable")]
        [TestCase(504, true, Description = "Gateway Timeout")]
        [TestCase(200, false, Description = "OK - Not retryable")]
        [TestCase(400, false, Description = "Bad Request - Not retryable")]
        [TestCase(401, false, Description = "Unauthorized - Not retryable")]
        [TestCase(403, false, Description = "Forbidden - Not retryable")]
        [TestCase(404, false, Description = "Not Found - Not retryable")]
        public void IsRetryableStatusCode_ReturnsExpectedResult(int statusCode, bool expected)
        {
            // Arrange
            var policy = RetryPolicy.Default;

            // Act
            var result = policy.IsRetryableStatusCode(statusCode);

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void IsRetryableStatusCode_CustomCodes_ReturnsExpectedResult()
        {
            // Arrange
            var policy = new RetryPolicy
            {
                RetryableStatusCodes = new HashSet<int> { 418, 503 }
            };

            // Act & Assert
            Assert.That(policy.IsRetryableStatusCode(418), Is.True);
            Assert.That(policy.IsRetryableStatusCode(503), Is.True);
            Assert.That(policy.IsRetryableStatusCode(500), Is.False);
        }

        #endregion

        #region CanRetry Tests

        [Test]
        [TestCase(0, 3, true)]
        [TestCase(1, 3, true)]
        [TestCase(2, 3, true)]
        [TestCase(3, 3, false)]
        [TestCase(4, 3, false)]
        [TestCase(0, 0, false)]
        public void CanRetry_ReturnsExpectedResult(int currentAttempt, int maxRetries, bool expected)
        {
            // Arrange
            var policy = new RetryPolicy { MaxRetries = maxRetries };

            // Act
            var result = policy.CanRetry(currentAttempt);

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        #endregion

        #region Static Factory Tests

        [Test]
        public void Default_ReturnsDefaultValues()
        {
            // Act
            var policy = RetryPolicy.Default;

            // Assert
            Assert.That(policy.MaxRetries, Is.EqualTo(3));
            Assert.That(policy.InitialDelayMs, Is.EqualTo(1000));
            Assert.That(policy.MaxDelayMs, Is.EqualTo(30000));
            Assert.That(policy.BackoffMultiplier, Is.EqualTo(2.0));
        }

        [Test]
        public void None_ReturnsNoRetryPolicy()
        {
            // Act
            var policy = RetryPolicy.None;

            // Assert
            Assert.That(policy.MaxRetries, Is.EqualTo(0));
            Assert.That(policy.CanRetry(0), Is.False);
        }

        [Test]
        public void Aggressive_ReturnsAggressiveValues()
        {
            // Act
            var policy = RetryPolicy.Aggressive;

            // Assert
            Assert.That(policy.MaxRetries, Is.EqualTo(5));
            Assert.That(policy.InitialDelayMs, Is.EqualTo(500));
            Assert.That(policy.MaxDelayMs, Is.EqualTo(10000));
            Assert.That(policy.BackoffMultiplier, Is.EqualTo(1.5));
        }

        #endregion

        #region Exponential Backoff Sequence Tests

        [Test]
        public void GetDelayMs_FullSequence_FollowsExponentialBackoff()
        {
            // Arrange
            var policy = new RetryPolicy
            {
                InitialDelayMs = 1000,
                BackoffMultiplier = 2.0,
                MaxDelayMs = 30000
            };

            // Act & Assert
            Assert.That(policy.GetDelayMs(0), Is.EqualTo(1000));   // 1s
            Assert.That(policy.GetDelayMs(1), Is.EqualTo(2000));   // 2s
            Assert.That(policy.GetDelayMs(2), Is.EqualTo(4000));   // 4s
            Assert.That(policy.GetDelayMs(3), Is.EqualTo(8000));   // 8s
            Assert.That(policy.GetDelayMs(4), Is.EqualTo(16000));  // 16s
            Assert.That(policy.GetDelayMs(5), Is.EqualTo(30000));  // 30s (capped at max)
            Assert.That(policy.GetDelayMs(6), Is.EqualTo(30000));  // 30s (capped at max)
        }

        #endregion
    }
}
