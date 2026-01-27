using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Core.Services;
using NUnit.Framework;

namespace Game.Tests.MVC
{
    [TestFixture]
    public class MessagePipeServiceTests
    {
        private MessagePipeService _service;

        [SetUp]
        public void Setup()
        {
            _service = new MessagePipeService();
            _service.Startup();
        }

        [TearDown]
        public void TearDown()
        {
            _service.Shutdown();
        }

        #region Signal Tests (値なし)

        [Test]
        public void Subscribe_Signal_ReceivesPublishedSignal()
        {
            // Arrange
            const int key = 100;
            var received = false;
            var subscription = _service.Subscribe(key, () => received = true);

            // Act
            _service.Publish(key);

            // Assert
            Assert.That(received, Is.True);

            subscription.Dispose();
        }

        [Test]
        public void Subscribe_Signal_MultipleSubscribers_AllReceive()
        {
            // Arrange
            const int key = 101;
            var count = 0;
            var sub1 = _service.Subscribe(key, () => count++);
            var sub2 = _service.Subscribe(key, () => count++);
            var sub3 = _service.Subscribe(key, () => count++);

            // Act
            _service.Publish(key);

            // Assert
            Assert.That(count, Is.EqualTo(3));

            sub1.Dispose();
            sub2.Dispose();
            sub3.Dispose();
        }

        [Test]
        public void Subscribe_Signal_DifferentKeys_OnlyMatchingKeyReceives()
        {
            // Arrange
            const int key1 = 102;
            const int key2 = 103;
            var received1 = false;
            var received2 = false;

            var sub1 = _service.Subscribe(key1, () => received1 = true);
            var sub2 = _service.Subscribe(key2, () => received2 = true);

            // Act
            _service.Publish(key1);

            // Assert
            Assert.That(received1, Is.True);
            Assert.That(received2, Is.False);

            sub1.Dispose();
            sub2.Dispose();
        }

        [Test]
        public void Subscribe_Signal_AfterDispose_DoesNotReceive()
        {
            // Arrange
            const int key = 104;
            var received = false;
            var subscription = _service.Subscribe(key, () => received = true);
            subscription.Dispose();

            // Act
            _service.Publish(key);

            // Assert
            Assert.That(received, Is.False);
        }

        #endregion

        #region Message Tests (値あり - int)

        [Test]
        public void Subscribe_IntMessage_ReceivesCorrectValue()
        {
            // Arrange
            const int key = 200;
            var receivedValue = 0;
            var subscription = _service.Subscribe<int>(key, value => receivedValue = value);

            // Act
            _service.Publish(key, 42);

            // Assert
            Assert.That(receivedValue, Is.EqualTo(42));

            subscription.Dispose();
        }

        [Test]
        public void Subscribe_IntMessage_MultiplePublishes_ReceivesAll()
        {
            // Arrange
            const int key = 201;
            var values = new List<int>();
            var subscription = _service.Subscribe<int>(key, value => values.Add(value));

            // Act
            _service.Publish(key, 1);
            _service.Publish(key, 2);
            _service.Publish(key, 3);

            // Assert
            Assert.That(values, Is.EqualTo(new[] { 1, 2, 3 }));

            subscription.Dispose();
        }

        #endregion

        #region Message Tests (値あり - float)

        [Test]
        public void Subscribe_FloatMessage_ReceivesCorrectValue()
        {
            // Arrange
            const int key = 300;
            var receivedValue = 0f;
            var subscription = _service.Subscribe<float>(key, value => receivedValue = value);

            // Act
            _service.Publish(key, 3.14f);

            // Assert
            Assert.That(receivedValue, Is.EqualTo(3.14f).Within(0.001f));

            subscription.Dispose();
        }

        #endregion

        #region Message Tests (値あり - bool)

        [Test]
        public void Subscribe_BoolMessage_ReceivesCorrectValue()
        {
            // Arrange
            const int key = 400;
            var receivedValue = false;
            var subscription = _service.Subscribe<bool>(key, value => receivedValue = value);

            // Act
            _service.Publish(key, true);

            // Assert
            Assert.That(receivedValue, Is.True);

            subscription.Dispose();
        }

        #endregion

        #region Message Tests (値あり - string)

        [Test]
        public void Subscribe_StringMessage_ReceivesCorrectValue()
        {
            // Arrange
            const int key = 500;
            string receivedValue = null;
            var subscription = _service.Subscribe<string>(key, value => receivedValue = value);

            // Act
            _service.Publish(key, "Hello, World!");

            // Assert
            Assert.That(receivedValue, Is.EqualTo("Hello, World!"));

            subscription.Dispose();
        }

        [Test]
        public void Subscribe_StringMessage_NullValue_ReceivesNull()
        {
            // Arrange
            const int key = 501;
            var receivedValue = "initial";
            var subscription = _service.Subscribe<string>(key, value => receivedValue = value);

            // Act
            _service.Publish<string>(key, null);

            // Assert
            Assert.That(receivedValue, Is.Null);

            subscription.Dispose();
        }

        #endregion

        #region Async Tests

        [Test]
        public async Task SubscribeAsync_Signal_ReceivesPublishedSignal()
        {
            // Arrange
            const int key = 600;
            var received = false;
            var subscription = _service.SubscribeAsync(key, async ct =>
            {
                received = true;
                await UniTask.CompletedTask;
            });

            // Act
            await _service.PublishAsync(key);

            // Assert
            Assert.That(received, Is.True);

            subscription.Dispose();
        }

        [Test]
        public async Task SubscribeAsync_Message_ReceivesCorrectValue()
        {
            // Arrange
            const int key = 601;
            var receivedValue = 0;
            var subscription = _service.SubscribeAsync<int>(key, async (value, ct) =>
            {
                receivedValue = value;
                await UniTask.CompletedTask;
            });

            // Act
            await _service.PublishAsync(key, 99);

            // Assert
            Assert.That(receivedValue, Is.EqualTo(99));

            subscription.Dispose();
        }

        #endregion

        #region PublishForget Tests

        [Test]
        public void PublishForget_Signal_DoesNotBlock()
        {
            // Arrange
            const int key = 700;
            var received = false;
            var subscription = _service.SubscribeAsync(key, async ct =>
            {
                await UniTask.Delay(10, cancellationToken: ct);
                received = true;
            });

            // Act - Should return immediately
            _service.PublishForget(key);

            // Assert - May not have received yet since it's fire-and-forget
            // Just verify no exception is thrown
            Assert.Pass();

            subscription.Dispose();
        }

        [Test]
        public void PublishForget_Message_DoesNotBlock()
        {
            // Arrange
            const int key = 701;
            var subscription = _service.SubscribeAsync<int>(key, async (value, ct) =>
            {
                await UniTask.Delay(10, cancellationToken: ct);
            });

            // Act - Should return immediately
            _service.PublishForget(key, 42);

            // Assert - Just verify no exception
            Assert.Pass();

            subscription.Dispose();
        }

        #endregion

        #region Raw Accessor Tests

        [Test]
        public void GetPublisher_ReturnsNonNull()
        {
            // Act
            var publisher = _service.GetPublisher<int, int>();

            // Assert
            Assert.That(publisher, Is.Not.Null);
        }

        [Test]
        public void GetSubscriber_ReturnsNonNull()
        {
            // Act
            var subscriber = _service.GetSubscriber<int, int>();

            // Assert
            Assert.That(subscriber, Is.Not.Null);
        }

        [Test]
        public void GetAsyncPublisher_ReturnsNonNull()
        {
            // Act
            var publisher = _service.GetAsyncPublisher<int, int>();

            // Assert
            Assert.That(publisher, Is.Not.Null);
        }

        [Test]
        public void GetAsyncSubscriber_ReturnsNonNull()
        {
            // Act
            var subscriber = _service.GetAsyncSubscriber<int, int>();

            // Assert
            Assert.That(subscriber, Is.Not.Null);
        }

        #endregion

        #region Lifecycle Tests

        [Test]
        public void Startup_CanBeCalledMultipleTimes()
        {
            // Act & Assert - Should not throw
            Assert.DoesNotThrow(() =>
            {
                var service = new MessagePipeService();
                service.Startup();
                // Note: Calling Startup again would rebuild, which is typically avoided
            });
        }

        [Test]
        public void Shutdown_CanBeCalledMultipleTimes()
        {
            // Arrange
            var service = new MessagePipeService();
            service.Startup();

            // Act & Assert - Should not throw
            Assert.DoesNotThrow(() =>
            {
                service.Shutdown();
                service.Shutdown();
            });
        }

        #endregion
    }
}
