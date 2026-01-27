using System;
using Game.Core.Services;
using NUnit.Framework;

namespace Game.Tests.MVC
{
    [TestFixture]
    public class GameServiceManagerTests
    {
        #region Test Service Classes

        private class TestService : IGameService
        {
            public bool IsStarted { get; private set; }
            public bool IsShutdown { get; private set; }
            public int StartupCallCount { get; private set; }
            public int ShutdownCallCount { get; private set; }

            public void Startup()
            {
                IsStarted = true;
                StartupCallCount++;
            }

            public void Shutdown()
            {
                IsShutdown = true;
                ShutdownCallCount++;
            }
        }

        private class AnotherTestService : IGameService
        {
            public bool IsStarted { get; private set; }

            public void Startup()
            {
                IsStarted = true;
            }

            public void Shutdown()
            {
            }
        }

        #endregion

        [SetUp]
        public void Setup()
        {
            // マネージャーをクリーンな状態にリセット
            GameServiceManager.Instance.StartUp();
        }

        [TearDown]
        public void TearDown()
        {
            GameServiceManager.Instance.Shutdown();
        }

        #region Singleton Tests

        [Test]
        public void Instance_ReturnsSameInstance()
        {
            // Act
            var instance1 = GameServiceManager.Instance;
            var instance2 = GameServiceManager.Instance;

            // Assert
            Assert.That(instance1, Is.SameAs(instance2));
        }

        #endregion

        #region Get Tests

        [Test]
        public void Get_FirstCall_CreatesAndStartsService()
        {
            // Act
            var service = GameServiceManager.Get<TestService>();

            // Assert
            Assert.That(service, Is.Not.Null);
            Assert.That(service.IsStarted, Is.True);
            Assert.That(service.StartupCallCount, Is.EqualTo(1));
        }

        [Test]
        public void Get_SecondCall_ReturnsSameInstance()
        {
            // Act
            var service1 = GameServiceManager.Get<TestService>();
            var service2 = GameServiceManager.Get<TestService>();

            // Assert
            Assert.That(service1, Is.SameAs(service2));
        }

        [Test]
        public void Get_SecondCall_DoesNotCallStartupAgain()
        {
            // Act
            var service = GameServiceManager.Get<TestService>();
            GameServiceManager.Get<TestService>();
            GameServiceManager.Get<TestService>();

            // Assert
            Assert.That(service.StartupCallCount, Is.EqualTo(1));
        }

        [Test]
        public void Get_DifferentTypes_ReturnsDifferentInstances()
        {
            // Act
            var service1 = GameServiceManager.Get<TestService>();
            var service2 = GameServiceManager.Get<AnotherTestService>();

            // Assert
            Assert.That(service1, Is.Not.SameAs(service2));
            Assert.That(service1.IsStarted, Is.True);
            Assert.That(service2.IsStarted, Is.True);
        }

        #endregion

        #region Add Tests

        [Test]
        public void Add_CreatesAndStartsService()
        {
            // Act
            GameServiceManager.Add<TestService>();

            // Assert - Get should return the already created service
            var service = GameServiceManager.Get<TestService>();
            Assert.That(service.StartupCallCount, Is.EqualTo(1));
        }

        [Test]
        public void Add_CalledTwice_DoesNotDuplicate()
        {
            // Act
            GameServiceManager.Add<TestService>();
            GameServiceManager.Add<TestService>();

            // Assert
            var service = GameServiceManager.Get<TestService>();
            Assert.That(service.StartupCallCount, Is.EqualTo(1));
        }

        #endregion

        #region Remove Tests

        [Test]
        public void Remove_ExistingService_CallsShutdown()
        {
            // Arrange
            var service = GameServiceManager.Get<TestService>();

            // Act
            GameServiceManager.Remove<TestService>();

            // Assert
            Assert.That(service.IsShutdown, Is.True);
            Assert.That(service.ShutdownCallCount, Is.EqualTo(1));
        }

        [Test]
        public void Remove_NonExistingService_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => GameServiceManager.Remove<TestService>());
        }

        [Test]
        public void Remove_ThenGet_CreatesNewInstance()
        {
            // Arrange
            var originalService = GameServiceManager.Get<TestService>();
            GameServiceManager.Remove<TestService>();

            // Act
            var newService = GameServiceManager.Get<TestService>();

            // Assert
            Assert.That(newService, Is.Not.SameAs(originalService));
            Assert.That(newService.StartupCallCount, Is.EqualTo(1));
        }

        #endregion

        #region Shutdown Tests

        [Test]
        public void Shutdown_CallsShutdownOnAllServices()
        {
            // Arrange
            var service1 = GameServiceManager.Get<TestService>();
            var service2 = GameServiceManager.Get<AnotherTestService>();

            // Act
            GameServiceManager.Instance.Shutdown();

            // Assert
            Assert.That(service1.IsShutdown, Is.True);
        }

        [Test]
        public void Shutdown_ClearsAllServices()
        {
            // Arrange
            var originalService = GameServiceManager.Get<TestService>();
            GameServiceManager.Instance.Shutdown();

            // Act - StartUp to reset, then Get should create new instance
            GameServiceManager.Instance.StartUp();
            var newService = GameServiceManager.Get<TestService>();

            // Assert
            Assert.That(newService, Is.Not.SameAs(originalService));
        }

        #endregion

        #region StartUp Tests

        [Test]
        public void StartUp_ClearsExistingServices()
        {
            // Arrange
            var originalService = GameServiceManager.Get<TestService>();

            // Act
            GameServiceManager.Instance.StartUp();
            var newService = GameServiceManager.Get<TestService>();

            // Assert
            Assert.That(newService, Is.Not.SameAs(originalService));
        }

        #endregion
    }
}
