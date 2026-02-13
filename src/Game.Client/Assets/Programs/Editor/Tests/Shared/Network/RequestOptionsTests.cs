using System;
using System.Collections.Generic;
using NUnit.Framework;
using Game.Shared.Services.Network.Models;
using Game.Shared.Services.Network.Policies;

namespace Game.Tests.Shared.Network
{
    [TestFixture]
    public class RequestOptionsTests
    {
        #region Default Factory Tests

        [Test]
        public void Default_ReturnsDefaultConfiguration()
        {
            // Act
            var options = RequestOptions.Default;

            // Assert
            Assert.That(options.RetryPolicy, Is.Not.Null);
            Assert.That(options.UseCache, Is.False);
            Assert.That(options.FallbackToCache, Is.True);
            Assert.That(options.TimeoutSeconds, Is.Null);
        }

        [Test]
        public void NoRetry_ReturnsNoRetryConfiguration()
        {
            // Act
            var options = RequestOptions.NoRetry;

            // Assert
            Assert.That(options.RetryPolicy, Is.Not.Null);
            Assert.That(options.RetryPolicy.MaxRetries, Is.EqualTo(0));
            Assert.That(options.UseCache, Is.False);
        }

        [Test]
        public void WithCache_ReturnsCorrectConfiguration()
        {
            // Arrange
            var duration = TimeSpan.FromMinutes(5);

            // Act
            var options = RequestOptions.WithCache(duration);

            // Assert
            Assert.That(options.UseCache, Is.True);
            Assert.That(options.CacheDuration, Is.EqualTo(duration));
            Assert.That(options.FallbackToCache, Is.True);
        }

        [Test]
        public void WithTimeout_ReturnsCorrectConfiguration()
        {
            // Act
            var options = RequestOptions.WithTimeout(30);

            // Assert
            Assert.That(options.TimeoutSeconds, Is.EqualTo(30));
            Assert.That(options.UseCache, Is.False);
        }

        #endregion

        #region GetEffectiveRetryPolicy Tests

        [Test]
        public void GetEffectiveRetryPolicy_WhenPolicySet_ReturnsThatPolicy()
        {
            // Arrange
            var customPolicy = RetryPolicy.Aggressive;
            var options = new RequestOptions { RetryPolicy = customPolicy };

            // Act
            var result = options.GetEffectiveRetryPolicy();

            // Assert
            Assert.That(result, Is.SameAs(customPolicy));
        }

        [Test]
        public void GetEffectiveRetryPolicy_WhenPolicyNull_ReturnsDefault()
        {
            // Arrange
            var options = new RequestOptions { RetryPolicy = null };

            // Act
            var result = options.GetEffectiveRetryPolicy();

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.MaxRetries, Is.EqualTo(RetryPolicy.Default.MaxRetries));
        }

        #endregion

        #region GetEffectiveTimeout Tests

        [Test]
        public void GetEffectiveTimeout_WhenTimeoutSet_ReturnsThatTimeout()
        {
            // Arrange
            var options = new RequestOptions { TimeoutSeconds = 30 };

            // Act
            var result = options.GetEffectiveTimeout(15);

            // Assert
            Assert.That(result, Is.EqualTo(30));
        }

        [Test]
        public void GetEffectiveTimeout_WhenTimeoutNull_ReturnsDefault()
        {
            // Arrange
            var options = new RequestOptions { TimeoutSeconds = null };

            // Act
            var result = options.GetEffectiveTimeout(15);

            // Assert
            Assert.That(result, Is.EqualTo(15));
        }

        #endregion

        #region AdditionalHeaders Tests

        [Test]
        public void AdditionalHeaders_CanBeSet()
        {
            // Arrange & Act
            var options = new RequestOptions
            {
                AdditionalHeaders = new Dictionary<string, string>
                {
                    { "X-Custom-Header", "CustomValue" },
                    { "X-Request-Id", "12345" }
                }
            };

            // Assert
            Assert.That(options.AdditionalHeaders, Has.Count.EqualTo(2));
            Assert.That(options.AdditionalHeaders["X-Custom-Header"], Is.EqualTo("CustomValue"));
            Assert.That(options.AdditionalHeaders["X-Request-Id"], Is.EqualTo("12345"));
        }

        [Test]
        public void AdditionalHeaders_DefaultIsNull()
        {
            // Arrange
            var options = new RequestOptions();

            // Assert
            Assert.That(options.AdditionalHeaders, Is.Null);
        }

        #endregion

        #region Cache Configuration Tests

        [Test]
        public void CacheKeyPrefix_CanBeSet()
        {
            // Arrange & Act
            var options = new RequestOptions
            {
                UseCache = true,
                CacheKeyPrefix = "ranking_"
            };

            // Assert
            Assert.That(options.CacheKeyPrefix, Is.EqualTo("ranking_"));
        }

        [Test]
        public void FallbackToCache_DefaultIsTrue()
        {
            // Arrange
            var options = new RequestOptions();

            // Assert
            Assert.That(options.FallbackToCache, Is.True);
        }

        [Test]
        public void FallbackToCache_CanBeDisabled()
        {
            // Arrange & Act
            var options = new RequestOptions { FallbackToCache = false };

            // Assert
            Assert.That(options.FallbackToCache, Is.False);
        }

        #endregion
    }
}
