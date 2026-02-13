using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Game.Shared.Services.Network.Policies;

namespace Game.Tests.Shared.Network
{
    [TestFixture]
    public class CircuitBreakerPolicyTests
    {
        private CircuitBreakerPolicy _policy;

        [SetUp]
        public void Setup()
        {
            _policy = new CircuitBreakerPolicy
            {
                FailureThreshold = 3,
                OpenDuration = TimeSpan.FromMilliseconds(100),
                SamplingDuration = TimeSpan.FromSeconds(10)
            };
        }

        #region Initial State Tests

        [Test]
        public void Constructor_InitialStateIsClosed()
        {
            // Assert
            Assert.That(_policy.State, Is.EqualTo(CircuitState.Closed));
        }

        [Test]
        public void Constructor_InitialFailureCountIsZero()
        {
            // Assert
            Assert.That(_policy.ConsecutiveFailures, Is.EqualTo(0));
        }

        [Test]
        public void Constructor_InitialOpenedAtIsNull()
        {
            // Assert
            Assert.That(_policy.OpenedAt, Is.Null);
        }

        #endregion

        #region CanExecute Tests

        [Test]
        public void CanExecute_WhenClosed_ReturnsTrue()
        {
            // Assert
            Assert.That(_policy.CanExecute(), Is.True);
        }

        [Test]
        public void CanExecute_WhenOpen_ReturnsFalse()
        {
            // Arrange - 閾値まで失敗を記録
            for (int i = 0; i < _policy.FailureThreshold; i++)
            {
                _policy.RecordFailure();
            }

            // Assert
            Assert.That(_policy.State, Is.EqualTo(CircuitState.Open));
            Assert.That(_policy.CanExecute(), Is.False);
        }

        [Test]
        public async Task CanExecute_WhenHalfOpen_ReturnsTrue()
        {
            // Arrange - Openにしてから待機
            for (int i = 0; i < _policy.FailureThreshold; i++)
            {
                _policy.RecordFailure();
            }

            // Open状態を確認
            Assert.That(_policy.State, Is.EqualTo(CircuitState.Open));

            // OpenDurationを待つ
            await Task.Delay(150);

            // Assert - HalfOpenに遷移
            Assert.That(_policy.State, Is.EqualTo(CircuitState.HalfOpen));
            Assert.That(_policy.CanExecute(), Is.True);
        }

        #endregion

        #region RecordFailure Tests

        [Test]
        public void RecordFailure_IncrementsConsecutiveFailures()
        {
            // Act
            _policy.RecordFailure();

            // Assert
            Assert.That(_policy.ConsecutiveFailures, Is.EqualTo(1));
        }

        [Test]
        public void RecordFailure_AtThreshold_TransitionsToOpen()
        {
            // Act
            for (int i = 0; i < _policy.FailureThreshold; i++)
            {
                _policy.RecordFailure();
            }

            // Assert
            Assert.That(_policy.State, Is.EqualTo(CircuitState.Open));
            Assert.That(_policy.OpenedAt, Is.Not.Null);
        }

        [Test]
        public void RecordFailure_BelowThreshold_StaysClosed()
        {
            // Act
            for (int i = 0; i < _policy.FailureThreshold - 1; i++)
            {
                _policy.RecordFailure();
            }

            // Assert
            Assert.That(_policy.State, Is.EqualTo(CircuitState.Closed));
        }

        [Test]
        public async Task RecordFailure_InHalfOpen_TransitionsBackToOpen()
        {
            // Arrange - HalfOpen状態にする
            for (int i = 0; i < _policy.FailureThreshold; i++)
            {
                _policy.RecordFailure();
            }
            await Task.Delay(150); // HalfOpenに遷移

            Assert.That(_policy.State, Is.EqualTo(CircuitState.HalfOpen));

            // Act - HalfOpen状態で失敗
            _policy.RecordFailure();

            // Assert - Openに戻る
            Assert.That(_policy.State, Is.EqualTo(CircuitState.Open));
        }

        #endregion

        #region RecordSuccess Tests

        [Test]
        public void RecordSuccess_ResetsConsecutiveFailures()
        {
            // Arrange
            _policy.RecordFailure();
            _policy.RecordFailure();

            // Act
            _policy.RecordSuccess();

            // Assert
            Assert.That(_policy.ConsecutiveFailures, Is.EqualTo(0));
        }

        [Test]
        public async Task RecordSuccess_InHalfOpen_TransitionsToClosed()
        {
            // Arrange - HalfOpen状態にする
            for (int i = 0; i < _policy.FailureThreshold; i++)
            {
                _policy.RecordFailure();
            }
            await Task.Delay(150);

            Assert.That(_policy.State, Is.EqualTo(CircuitState.HalfOpen));

            // Act
            _policy.RecordSuccess();

            // Assert
            Assert.That(_policy.State, Is.EqualTo(CircuitState.Closed));
            Assert.That(_policy.OpenedAt, Is.Null);
        }

        #endregion

        #region Reset Tests

        [Test]
        public void Reset_TransitionsToClosed()
        {
            // Arrange - Open状態にする
            for (int i = 0; i < _policy.FailureThreshold; i++)
            {
                _policy.RecordFailure();
            }

            // Act
            _policy.Reset();

            // Assert
            Assert.That(_policy.State, Is.EqualTo(CircuitState.Closed));
            Assert.That(_policy.ConsecutiveFailures, Is.EqualTo(0));
            Assert.That(_policy.OpenedAt, Is.Null);
        }

        #endregion

        #region GetRemainingOpenTime Tests

        [Test]
        public void GetRemainingOpenTime_WhenClosed_ReturnsZero()
        {
            // Assert
            Assert.That(_policy.GetRemainingOpenTime(), Is.EqualTo(TimeSpan.Zero));
        }

        [Test]
        public void GetRemainingOpenTime_WhenOpen_ReturnsPositiveValue()
        {
            // Arrange
            for (int i = 0; i < _policy.FailureThreshold; i++)
            {
                _policy.RecordFailure();
            }

            // Act
            var remaining = _policy.GetRemainingOpenTime();

            // Assert
            Assert.That(remaining, Is.GreaterThan(TimeSpan.Zero));
            Assert.That(remaining, Is.LessThanOrEqualTo(_policy.OpenDuration));
        }

        #endregion

        #region SamplingDuration Tests

        [Test]
        public async Task RecordFailure_AfterSamplingDuration_ResetsCount()
        {
            // Arrange - 短いサンプリング期間を設定
            _policy.SamplingDuration = TimeSpan.FromMilliseconds(50);
            _policy.RecordFailure();
            _policy.RecordFailure();

            Assert.That(_policy.ConsecutiveFailures, Is.EqualTo(2));

            // Act - サンプリング期間を待つ
            await Task.Delay(100);
            _policy.RecordFailure();

            // Assert - カウントがリセットされて1になる
            Assert.That(_policy.ConsecutiveFailures, Is.EqualTo(1));
        }

        #endregion

        #region Static Factory Tests

        [Test]
        public void Default_ReturnsDefaultValues()
        {
            // Act
            var policy = CircuitBreakerPolicy.Default;

            // Assert
            Assert.That(policy.FailureThreshold, Is.EqualTo(5));
            Assert.That(policy.OpenDuration, Is.EqualTo(TimeSpan.FromSeconds(30)));
        }

        [Test]
        public void Sensitive_ReturnsLowerThreshold()
        {
            // Act
            var policy = CircuitBreakerPolicy.Sensitive;

            // Assert
            Assert.That(policy.FailureThreshold, Is.EqualTo(3));
            Assert.That(policy.OpenDuration, Is.EqualTo(TimeSpan.FromSeconds(60)));
        }

        [Test]
        public void Tolerant_ReturnsHigherThreshold()
        {
            // Act
            var policy = CircuitBreakerPolicy.Tolerant;

            // Assert
            Assert.That(policy.FailureThreshold, Is.EqualTo(10));
            Assert.That(policy.OpenDuration, Is.EqualTo(TimeSpan.FromSeconds(15)));
        }

        [Test]
        public void Disabled_NeverOpens()
        {
            // Arrange
            var policy = CircuitBreakerPolicy.Disabled;

            // Act - 大量の失敗を記録
            for (int i = 0; i < 100; i++)
            {
                policy.RecordFailure();
            }

            // Assert - 常にClosed
            Assert.That(policy.State, Is.EqualTo(CircuitState.Closed));
            Assert.That(policy.CanExecute(), Is.True);
        }

        #endregion

        #region Thread Safety Tests

        [Test]
        public void RecordFailure_ConcurrentCalls_ThreadSafe()
        {
            // Arrange
            var policy = new CircuitBreakerPolicy { FailureThreshold = 100 };
            var tasks = new Task[10];

            // Act - 並列で失敗を記録
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < 10; j++)
                    {
                        policy.RecordFailure();
                    }
                });
            }

            Task.WaitAll(tasks);

            // Assert - 例外が発生せず、カウントが100になる
            Assert.That(policy.ConsecutiveFailures, Is.EqualTo(100));
        }

        #endregion
    }
}
