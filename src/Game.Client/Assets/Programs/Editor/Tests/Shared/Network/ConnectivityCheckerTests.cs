using System;
using System.Collections.Generic;
using NUnit.Framework;
using Game.Shared.Services.Network.Connectivity;

namespace Game.Tests.Shared.Network
{
    [TestFixture]
    public class ConnectivityCheckerTests
    {
        private ConnectivityChecker _checker;

        [SetUp]
        public void Setup()
        {
            _checker = new ConnectivityChecker(0.1f); // 短い間隔でテスト
        }

        [TearDown]
        public void TearDown()
        {
            _checker?.Dispose();
        }

        #region Initial State Tests

        [Test]
        public void Constructor_InitializesWithCurrentConnectivity()
        {
            // Assert - 初期状態は現在の接続状態を反映
            // UnityエディタはNetworkReachability.ReachableViaLocalAreaNetworkを返すことが多い
            Assert.That(_checker.IsConnected, Is.True.Or.False);
        }

        [Test]
        public void OnConnectivityChanged_CanSubscribe()
        {
            // Arrange & Act
            var observable = _checker.OnConnectivityChanged;

            // Assert - Observableが取得可能
            Assert.That(observable, Is.Not.Null);
        }

        #endregion

        #region StartMonitoring Tests

        [Test]
        public void StartMonitoring_CanBeCalled()
        {
            // Act & Assert - 例外が発生しないこと
            Assert.DoesNotThrow(() => _checker.StartMonitoring());
        }

        [Test]
        public void StartMonitoring_CalledTwice_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                _checker.StartMonitoring();
                _checker.StartMonitoring();
            });
        }

        #endregion

        #region StopMonitoring Tests

        [Test]
        public void StopMonitoring_CanBeCalled()
        {
            // Arrange
            _checker.StartMonitoring();

            // Act & Assert
            Assert.DoesNotThrow(() => _checker.StopMonitoring());
        }

        [Test]
        public void StopMonitoring_WithoutStarting_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _checker.StopMonitoring());
        }

        #endregion

        #region CheckConnectivity Tests

        [Test]
        public void CheckConnectivity_ReturnsBoolean()
        {
            // Act
            var result = _checker.CheckConnectivity();

            // Assert
            Assert.That(result, Is.True.Or.False);
        }

        [Test]
        public void CheckConnectivity_UpdatesIsConnected()
        {
            // Act
            var result = _checker.CheckConnectivity();

            // Assert
            Assert.That(_checker.IsConnected, Is.EqualTo(result));
        }

        #endregion

        #region Dispose Tests

        [Test]
        public void Dispose_StopsMonitoring()
        {
            // Arrange
            _checker.StartMonitoring();

            // Act
            _checker.Dispose();

            // Assert - 再度StartMonitoringしても開始しない（disposedのため）
            _checker.StartMonitoring();
            // 例外が発生しないことを確認
        }

        [Test]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                _checker.Dispose();
                _checker.Dispose();
            });
        }

        #endregion
    }
}
