using System;
using System.Collections.Generic;
using Game.MVP.Core.Services;
using NUnit.Framework;
using VContainer.Unity;

namespace Game.Tests.MVP
{
    [TestFixture]
    public class TickableServiceTests
    {
        private TickableService _service;

        [SetUp]
        public void Setup()
        {
            _service = new TickableService();
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
        }

        #region Register/Unregister Tests - ITickable

        [Test]
        public void Register_ITickable_ExecutesOnTick()
        {
            // Arrange
            var executed = false;
            _service.Register<ITickable>(() => executed = true);

            // Act
            ((ITickable)_service).Tick();

            // Assert
            Assert.That(executed, Is.True);
        }

        [Test]
        public void Register_ITickable_MultipleActions_ExecutesAll()
        {
            // Arrange
            var count = 0;
            _service.Register<ITickable>(() => count++);
            _service.Register<ITickable>(() => count++);
            _service.Register<ITickable>(() => count++);

            // Act
            ((ITickable)_service).Tick();

            // Assert
            Assert.That(count, Is.EqualTo(3));
        }

        [Test]
        public void Unregister_ITickable_DoesNotExecute()
        {
            // Arrange
            var executed = false;
            Action action = () => executed = true;
            _service.Register<ITickable>(action);
            _service.Unregister<ITickable>(action);

            // Act
            ((ITickable)_service).Tick();

            // Assert
            Assert.That(executed, Is.False);
        }

        [Test]
        public void Register_ITickable_NullAction_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _service.Register<ITickable>(null));
        }

        [Test]
        public void Unregister_ITickable_NullAction_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _service.Unregister<ITickable>(null));
        }

        #endregion

        #region Register/Unregister Tests - IFixedTickable

        [Test]
        public void Register_IFixedTickable_ExecutesOnFixedTick()
        {
            // Arrange
            var executed = false;
            _service.Register<IFixedTickable>(() => executed = true);

            // Act
            ((IFixedTickable)_service).FixedTick();

            // Assert
            Assert.That(executed, Is.True);
        }

        [Test]
        public void Unregister_IFixedTickable_DoesNotExecute()
        {
            // Arrange
            var executed = false;
            Action action = () => executed = true;
            _service.Register<IFixedTickable>(action);
            _service.Unregister<IFixedTickable>(action);

            // Act
            ((IFixedTickable)_service).FixedTick();

            // Assert
            Assert.That(executed, Is.False);
        }

        #endregion

        #region Register/Unregister Tests - ILateTickable

        [Test]
        public void Register_ILateTickable_ExecutesOnLateTick()
        {
            // Arrange
            var executed = false;
            _service.Register<ILateTickable>(() => executed = true);

            // Act
            ((ILateTickable)_service).LateTick();

            // Assert
            Assert.That(executed, Is.True);
        }

        [Test]
        public void Unregister_ILateTickable_DoesNotExecute()
        {
            // Arrange
            var executed = false;
            Action action = () => executed = true;
            _service.Register<ILateTickable>(action);
            _service.Unregister<ILateTickable>(action);

            // Act
            ((ILateTickable)_service).LateTick();

            // Assert
            Assert.That(executed, Is.False);
        }

        #endregion

        #region Execution Order Tests

        [Test]
        public void Tick_ExecutesActionsInRegistrationOrder()
        {
            // Arrange
            var executionOrder = new List<int>();
            _service.Register<ITickable>(() => executionOrder.Add(1));
            _service.Register<ITickable>(() => executionOrder.Add(2));
            _service.Register<ITickable>(() => executionOrder.Add(3));

            // Act
            ((ITickable)_service).Tick();

            // Assert
            Assert.That(executionOrder, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void MultipleTicks_ExecutesEachTime()
        {
            // Arrange
            var count = 0;
            _service.Register<ITickable>(() => count++);

            // Act
            ((ITickable)_service).Tick();
            ((ITickable)_service).Tick();
            ((ITickable)_service).Tick();

            // Assert
            Assert.That(count, Is.EqualTo(3));
        }

        #endregion

        #region Pending Add/Remove During Iteration Tests

        [Test]
        public void Register_DuringTick_ExecutesOnNextTick()
        {
            // Arrange
            var lateRegisteredExecuted = false;
            Action lateAction = () => lateRegisteredExecuted = true;

            _service.Register<ITickable>(() =>
            {
                // Register during iteration
                _service.Register<ITickable>(lateAction);
            });

            // Act - First tick: registers the late action
            ((ITickable)_service).Tick();

            // Assert - Late action should not have executed yet during first tick
            // but should be ready for next tick

            // Act - Second tick: late action should execute
            lateRegisteredExecuted = false; // Reset
            ((ITickable)_service).Tick();

            // Assert
            Assert.That(lateRegisteredExecuted, Is.True);
        }

        [Test]
        public void Unregister_DuringTick_StopsOnNextTick()
        {
            // Arrange
            var count = 0;
            Action countAction = () => count++;

            _service.Register<ITickable>(() =>
            {
                // Unregister during iteration
                _service.Unregister<ITickable>(countAction);
            });
            _service.Register<ITickable>(countAction);

            // Act - First tick: countAction executes, then gets unregistered
            ((ITickable)_service).Tick();
            var countAfterFirstTick = count;

            // Act - Second tick: countAction should not execute
            ((ITickable)_service).Tick();

            // Assert
            Assert.That(countAfterFirstTick, Is.EqualTo(1));
            Assert.That(count, Is.EqualTo(1)); // Still 1, not incremented
        }

        #endregion

        #region Separation Tests

        [Test]
        public void DifferentTickTypes_AreSeparate()
        {
            // Arrange
            var tickCount = 0;
            var fixedTickCount = 0;
            var lateTickCount = 0;

            _service.Register<ITickable>(() => tickCount++);
            _service.Register<IFixedTickable>(() => fixedTickCount++);
            _service.Register<ILateTickable>(() => lateTickCount++);

            // Act - Only call Tick
            ((ITickable)_service).Tick();

            // Assert
            Assert.That(tickCount, Is.EqualTo(1));
            Assert.That(fixedTickCount, Is.EqualTo(0));
            Assert.That(lateTickCount, Is.EqualTo(0));
        }

        [Test]
        public void AllTickTypes_CanBeCalledIndependently()
        {
            // Arrange
            var tickCount = 0;
            var fixedTickCount = 0;
            var lateTickCount = 0;

            _service.Register<ITickable>(() => tickCount++);
            _service.Register<IFixedTickable>(() => fixedTickCount++);
            _service.Register<ILateTickable>(() => lateTickCount++);

            // Act
            ((ITickable)_service).Tick();
            ((ITickable)_service).Tick();
            ((IFixedTickable)_service).FixedTick();
            ((ILateTickable)_service).LateTick();
            ((ILateTickable)_service).LateTick();
            ((ILateTickable)_service).LateTick();

            // Assert
            Assert.That(tickCount, Is.EqualTo(2));
            Assert.That(fixedTickCount, Is.EqualTo(1));
            Assert.That(lateTickCount, Is.EqualTo(3));
        }

        #endregion

        #region Invalid Type Tests

        [Test]
        public void Register_UnsupportedType_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
            {
                _service.Register<IDisposable>(() => { });
            });
        }

        [Test]
        public void Unregister_UnsupportedType_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
            {
                _service.Unregister<IDisposable>(() => { });
            });
        }

        #endregion

        #region Dispose Tests

        [Test]
        public void Dispose_ClearsAllActions()
        {
            // Arrange
            var tickCount = 0;
            var fixedTickCount = 0;
            var lateTickCount = 0;

            _service.Register<ITickable>(() => tickCount++);
            _service.Register<IFixedTickable>(() => fixedTickCount++);
            _service.Register<ILateTickable>(() => lateTickCount++);

            // Act
            _service.Dispose();
            ((ITickable)_service).Tick();
            ((IFixedTickable)_service).FixedTick();
            ((ILateTickable)_service).LateTick();

            // Assert
            Assert.That(tickCount, Is.EqualTo(0));
            Assert.That(fixedTickCount, Is.EqualTo(0));
            Assert.That(lateTickCount, Is.EqualTo(0));
        }

        [Test]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                _service.Dispose();
                _service.Dispose();
            });
        }

        #endregion
    }
}
