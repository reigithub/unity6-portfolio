using System;
using Game.Shared.Services.Network;
using Game.Shared.Services.Network.Connectivity;
using Game.Shared.Services.Network.Policies;
using NSubstitute;
using NUnit.Framework;
using R3;

namespace Game.Tests.Shared.Network
{
    [TestFixture]
    public class NetworkServiceTests
    {
        private IConnectivityChecker _mockConnectivityChecker;
        private CircuitBreakerPolicy _circuitBreaker;
        private NetworkService _service;
        private ReactiveProperty<bool> _connectivityProperty;

        [SetUp]
        public void Setup()
        {
            _mockConnectivityChecker = Substitute.For<IConnectivityChecker>();

            _connectivityProperty = new ReactiveProperty<bool>(true);
            _mockConnectivityChecker.IsConnected.Returns(true);
            _mockConnectivityChecker.OnConnectivityChanged.Returns(_connectivityProperty.DistinctUntilChanged());

            _circuitBreaker = new CircuitBreakerPolicy
            {
                FailureThreshold = 3,
                OpenDuration = TimeSpan.FromSeconds(30)
            };

            _service = new NetworkService(_mockConnectivityChecker, _circuitBreaker);
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            _connectivityProperty?.Dispose();
        }

        #region Constructor Tests

        [Test]
        public void Constructor_StartsMonitoring()
        {
            // Assert
            _mockConnectivityChecker.Received(1).StartMonitoring();
        }

        [Test]
        public void Constructor_WithNullConnectivityChecker_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.That(() => new NetworkService(null, _circuitBreaker),
                Throws.ArgumentNullException);
        }

        [Test]
        public void Constructor_WithNullCircuitBreaker_UsesDefault()
        {
            // Arrange & Act
            using var service = new NetworkService(_mockConnectivityChecker, null);

            // Assert - デフォルトのサーキットブレーカーが使用される
            Assert.That(service.CircuitState, Is.EqualTo(CircuitState.Closed));
            Assert.That(service.CanExecute, Is.True);
        }

        #endregion

        #region IsConnected Tests

        [Test]
        public void IsConnected_ReturnsConnectivityCheckerValue_WhenTrue()
        {
            // Arrange
            _mockConnectivityChecker.IsConnected.Returns(true);

            // Act & Assert
            Assert.That(_service.IsConnected, Is.True);
        }

        [Test]
        public void IsConnected_ReturnsConnectivityCheckerValue_WhenFalse()
        {
            // Arrange
            _mockConnectivityChecker.IsConnected.Returns(false);

            // Act & Assert
            Assert.That(_service.IsConnected, Is.False);
        }

        [Test]
        public void OnConnectivityChanged_ReturnsObservable()
        {
            // Act & Assert
            Assert.That(_service.OnConnectivityChanged, Is.Not.Null);
        }

        #endregion

        #region CanExecute Tests

        [Test]
        public void CanExecute_ReturnsTrue_WhenCircuitClosed()
        {
            // Assert
            Assert.That(_service.CanExecute, Is.True);
        }

        [Test]
        public void CanExecute_ReturnsFalse_WhenCircuitOpen()
        {
            // Arrange - サーキットを開く
            for (int i = 0; i < 3; i++)
            {
                _service.RecordFailure();
            }

            // Act & Assert
            Assert.That(_service.CanExecute, Is.False);
        }

        [Test]
        public void CanExecute_ReturnsTrue_WhenCircuitHalfOpen()
        {
            // Arrange - 短いOpenDurationでサーキットを開く
            var shortCircuitBreaker = new CircuitBreakerPolicy
            {
                FailureThreshold = 1,
                OpenDuration = TimeSpan.FromMilliseconds(1)
            };
            using var service = new NetworkService(_mockConnectivityChecker, shortCircuitBreaker);

            service.RecordFailure(); // Open
            System.Threading.Thread.Sleep(10); // HalfOpenに遷移

            // Act & Assert
            Assert.That(service.CanExecute, Is.True);
        }

        #endregion

        #region CircuitState Tests

        [Test]
        public void CircuitState_ReturnsClosedByDefault()
        {
            // Assert
            Assert.That(_service.CircuitState, Is.EqualTo(CircuitState.Closed));
        }

        [Test]
        public void CircuitState_ReturnsOpen_AfterThresholdFailures()
        {
            // Arrange & Act
            for (int i = 0; i < 3; i++)
            {
                _service.RecordFailure();
            }

            // Assert
            Assert.That(_service.CircuitState, Is.EqualTo(CircuitState.Open));
        }

        #endregion

        #region RecordSuccess Tests

        [Test]
        public void RecordSuccess_ClosesCircuit_WhenHalfOpen()
        {
            // Arrange - 短いOpenDurationでサーキットを開く
            var shortCircuitBreaker = new CircuitBreakerPolicy
            {
                FailureThreshold = 1,
                OpenDuration = TimeSpan.FromMilliseconds(1)
            };
            using var service = new NetworkService(_mockConnectivityChecker, shortCircuitBreaker);

            service.RecordFailure(); // Open
            System.Threading.Thread.Sleep(10); // HalfOpenに遷移

            // Act
            service.RecordSuccess();

            // Assert
            Assert.That(service.CircuitState, Is.EqualTo(CircuitState.Closed));
        }

        [Test]
        public void RecordSuccess_MaintainsClosed_WhenAlreadyClosed()
        {
            // Arrange
            Assert.That(_service.CircuitState, Is.EqualTo(CircuitState.Closed));

            // Act
            _service.RecordSuccess();

            // Assert
            Assert.That(_service.CircuitState, Is.EqualTo(CircuitState.Closed));
        }

        #endregion

        #region RecordFailure Tests

        [Test]
        public void RecordFailure_IncrementsFailureCount()
        {
            // Arrange - 閾値未満の失敗
            _service.RecordFailure();
            _service.RecordFailure();

            // Assert - まだClosedのまま
            Assert.That(_service.CircuitState, Is.EqualTo(CircuitState.Closed));
        }

        [Test]
        public void RecordFailure_OpensCircuit_WhenThresholdReached()
        {
            // Arrange & Act
            for (int i = 0; i < 3; i++)
            {
                _service.RecordFailure();
            }

            // Assert
            Assert.That(_service.CircuitState, Is.EqualTo(CircuitState.Open));
        }

        [Test]
        public void RecordFailure_ReopensCircuit_WhenHalfOpen()
        {
            // Arrange - 短いOpenDurationでサーキットを開く
            var shortCircuitBreaker = new CircuitBreakerPolicy
            {
                FailureThreshold = 1,
                OpenDuration = TimeSpan.FromMilliseconds(1)
            };
            using var service = new NetworkService(_mockConnectivityChecker, shortCircuitBreaker);

            service.RecordFailure(); // Open
            System.Threading.Thread.Sleep(10); // HalfOpenに遷移

            Assert.That(service.CircuitState, Is.EqualTo(CircuitState.HalfOpen));

            // Act
            service.RecordFailure();

            // Assert
            Assert.That(service.CircuitState, Is.EqualTo(CircuitState.Open));
        }

        #endregion

        #region ResetCircuitBreaker Tests

        [Test]
        public void ResetCircuitBreaker_ResetsToClosedState()
        {
            // Arrange - サーキットを開く
            for (int i = 0; i < 3; i++)
            {
                _service.RecordFailure();
            }
            Assert.That(_service.CircuitState, Is.EqualTo(CircuitState.Open));

            // Act
            _service.ResetCircuitBreaker();

            // Assert
            Assert.That(_service.CircuitState, Is.EqualTo(CircuitState.Closed));
            Assert.That(_service.CanExecute, Is.True);
        }

        [Test]
        public void ResetCircuitBreaker_HasNoEffect_WhenAlreadyClosed()
        {
            // Arrange
            Assert.That(_service.CircuitState, Is.EqualTo(CircuitState.Closed));

            // Act
            _service.ResetCircuitBreaker();

            // Assert
            Assert.That(_service.CircuitState, Is.EqualTo(CircuitState.Closed));
        }

        #endregion

        #region OnCircuitStateChanged Tests

        [Test]
        public void OnCircuitStateChanged_EmitsEvent_WhenStateChanges()
        {
            // Arrange
            CircuitState? emittedState = null;
            _service.OnCircuitStateChanged.Subscribe(state => emittedState = state);

            // Act - サーキットを開く
            for (int i = 0; i < 3; i++)
            {
                _service.RecordFailure();
            }

            // Assert
            Assert.That(emittedState, Is.EqualTo(CircuitState.Open));
        }

        [Test]
        public void OnCircuitStateChanged_DoesNotEmit_WhenStateUnchanged()
        {
            // Arrange
            int emitCount = 0;
            _service.OnCircuitStateChanged.Subscribe(_ => emitCount++);

            // Act - 閾値未満の失敗（状態変化なし）
            _service.RecordFailure();
            _service.RecordFailure();

            // Assert
            Assert.That(emitCount, Is.EqualTo(0));
        }

        [Test]
        public void OnCircuitStateChanged_EmitsClosedState_WhenReset()
        {
            // Arrange
            CircuitState? lastEmittedState = null;
            _service.OnCircuitStateChanged.Subscribe(state => lastEmittedState = state);

            // サーキットを開く
            for (int i = 0; i < 3; i++)
            {
                _service.RecordFailure();
            }
            Assert.That(lastEmittedState, Is.EqualTo(CircuitState.Open));

            // Act
            _service.ResetCircuitBreaker();

            // Assert
            Assert.That(lastEmittedState, Is.EqualTo(CircuitState.Closed));
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

        [Test]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            // Act & Assert - 例外が発生しないこと
            Assert.DoesNotThrow(() =>
            {
                _service.Dispose();
                _service.Dispose();
            });

            // StopMonitoringは1回のみ呼ばれる
            _mockConnectivityChecker.Received(1).StopMonitoring();
        }

        #endregion
    }
}
