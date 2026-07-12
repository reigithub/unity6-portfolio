using System;
using Game.Core.Services;
using NUnit.Framework;

namespace Game.Tests.MVC
{
    [TestFixture]
    public class GameServiceManagerTests
    {
        #region Test Service Classes

        private interface ITestService
        {
            public bool IsStarted { get;}
            public bool IsShutdown { get;}
            public int StartupCallCount { get;}
            public int ShutdownCallCount { get; }
        }

        private class TestService : ITestService, IGameService
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

        private interface IAnotherTestService
        {
            public bool IsStarted { get; }
        }

        private class AnotherTestService : IAnotherTestService, IGameService
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
            GameServiceManager.StartUp();
            GameServiceManager.Register<ITestService, TestService>(new TestService());
            GameServiceManager.Register<IAnotherTestService, AnotherTestService>(new AnotherTestService());
        }

        [TearDown]
        public void TearDown()
        {
            GameServiceManager.Shutdown();
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
            var service = GameServiceManager.Resolve<ITestService>();

            // Assert
            Assert.That(service, Is.Not.Null);
            Assert.That(service.IsStarted, Is.True);
            Assert.That(service.StartupCallCount, Is.EqualTo(1));
        }

        [Test]
        public void Get_SecondCall_ReturnsSameInstance()
        {
            // Act
            var service1 = GameServiceManager.Resolve<ITestService>();
            var service2 = GameServiceManager.Resolve<ITestService>();

            // Assert
            Assert.That(service1, Is.SameAs(service2));
        }

        [Test]
        public void Get_SecondCall_DoesNotCallStartupAgain()
        {
            // Act
            var service = GameServiceManager.Resolve<ITestService>();
            GameServiceManager.Resolve<ITestService>();
            GameServiceManager.Resolve<ITestService>();

            // Assert
            Assert.That(service.StartupCallCount, Is.EqualTo(1));
        }

        [Test]
        public void Get_DifferentTypes_ReturnsDifferentInstances()
        {
            // Act
            var service1 = GameServiceManager.Resolve<ITestService>();
            var service2 = GameServiceManager.Resolve<IAnotherTestService>();

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
            GameServiceManager.Register<ITestService, TestService>(new TestService());

            // Assert - Get should return the already created service
            var service = GameServiceManager.Resolve<ITestService>();
            Assert.That(service.StartupCallCount, Is.EqualTo(1));
        }

        [Test]
        public void Add_CalledTwice_DoesNotDuplicate()
        {
            // Act
            var s = new TestService();
            GameServiceManager.Register<ITestService, TestService>(s);
            GameServiceManager.Register<ITestService, TestService>(s);

            // Assert
            var service = GameServiceManager.Resolve<ITestService>();
            Assert.That(service.StartupCallCount, Is.EqualTo(1));
        }

        #endregion

        #region Remove Tests

        [Test]
        public void Remove_ExistingService_CallsShutdown()
        {
            // Arrange
            var service = GameServiceManager.Resolve<ITestService>();

            // Act
            GameServiceManager.Unregister<ITestService>();

            // Assert
            Assert.That(service.IsShutdown, Is.True);
            Assert.That(service.ShutdownCallCount, Is.EqualTo(1));
        }

        [Test]
        public void Remove_NonExistingService_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => GameServiceManager.Unregister<ITestService>());
        }

        #endregion

        #region Shutdown Tests

        [Test]
        public void Shutdown_CallsShutdownOnAllServices()
        {
            // Arrange
            var service1 = GameServiceManager.Resolve<ITestService>();

            // Act
            GameServiceManager.Shutdown();

            // Assert
            Assert.That(service1.IsShutdown, Is.True);
        }

        #endregion
    }
}
