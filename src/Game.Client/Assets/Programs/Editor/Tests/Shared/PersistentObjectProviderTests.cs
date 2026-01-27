using System;
using Game.Shared.Services;
using NUnit.Framework;

namespace Game.Tests.Shared
{
    [TestFixture]
    public class PersistentObjectProviderTests
    {
        private PersistentObjectProvider _provider;

        #region Test Classes

        private class TestObject
        {
            public string Name { get; set; }
            public int Value { get; set; }
        }

        private class AnotherTestObject
        {
            public float Data { get; set; }
        }

        private interface ITestInterface
        {
            void DoSomething();
        }

        private class TestImplementation : ITestInterface
        {
            public bool WasCalled { get; private set; }
            public void DoSomething() => WasCalled = true;
        }

        #endregion

        [SetUp]
        public void Setup()
        {
            _provider = new PersistentObjectProvider();
        }

        [TearDown]
        public void TearDown()
        {
            _provider.Clear();
        }

        #region Register Tests

        [Test]
        public void Register_ValidObject_Succeeds()
        {
            // Arrange
            var obj = new TestObject { Name = "Test", Value = 42 };

            // Act & Assert
            Assert.DoesNotThrow(() => _provider.Register(obj));
        }

        [Test]
        public void Register_NullObject_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _provider.Register<TestObject>(null));
        }

        [Test]
        public void Register_SameTypeTwice_ThrowsInvalidOperationException()
        {
            // Arrange
            var obj1 = new TestObject { Name = "First" };
            var obj2 = new TestObject { Name = "Second" };
            _provider.Register(obj1);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _provider.Register(obj2));
        }

        [Test]
        public void Register_DifferentTypes_Succeeds()
        {
            // Arrange
            var obj1 = new TestObject { Name = "Test" };
            var obj2 = new AnotherTestObject { Data = 3.14f };

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                _provider.Register(obj1);
                _provider.Register(obj2);
            });
        }

        [Test]
        public void Register_Interface_Succeeds()
        {
            // Arrange
            var impl = new TestImplementation();

            // Act & Assert
            Assert.DoesNotThrow(() => _provider.Register<ITestInterface>(impl));
        }

        #endregion

        #region Get Tests

        [Test]
        public void Get_RegisteredObject_ReturnsCorrectInstance()
        {
            // Arrange
            var obj = new TestObject { Name = "Test", Value = 42 };
            _provider.Register(obj);

            // Act
            var retrieved = _provider.Get<TestObject>();

            // Assert
            Assert.That(retrieved, Is.SameAs(obj));
            Assert.That(retrieved.Name, Is.EqualTo("Test"));
            Assert.That(retrieved.Value, Is.EqualTo(42));
        }

        [Test]
        public void Get_UnregisteredType_ThrowsInvalidOperationException()
        {
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _provider.Get<TestObject>());
        }

        [Test]
        public void Get_Interface_ReturnsImplementation()
        {
            // Arrange
            var impl = new TestImplementation();
            _provider.Register<ITestInterface>(impl);

            // Act
            var retrieved = _provider.Get<ITestInterface>();

            // Assert
            Assert.That(retrieved, Is.SameAs(impl));
        }

        [Test]
        public void Get_CalledMultipleTimes_ReturnsSameInstance()
        {
            // Arrange
            var obj = new TestObject { Name = "Test" };
            _provider.Register(obj);

            // Act
            var retrieved1 = _provider.Get<TestObject>();
            var retrieved2 = _provider.Get<TestObject>();
            var retrieved3 = _provider.Get<TestObject>();

            // Assert
            Assert.That(retrieved1, Is.SameAs(retrieved2));
            Assert.That(retrieved2, Is.SameAs(retrieved3));
        }

        #endregion

        #region TryGet Tests

        [Test]
        public void TryGet_RegisteredObject_ReturnsTrueAndInstance()
        {
            // Arrange
            var obj = new TestObject { Name = "Test" };
            _provider.Register(obj);

            // Act
            var result = _provider.TryGet<TestObject>(out var retrieved);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(retrieved, Is.SameAs(obj));
        }

        [Test]
        public void TryGet_UnregisteredType_ReturnsFalseAndNull()
        {
            // Act
            var result = _provider.TryGet<TestObject>(out var retrieved);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(retrieved, Is.Null);
        }

        [Test]
        public void TryGet_Interface_ReturnsTrueAndImplementation()
        {
            // Arrange
            var impl = new TestImplementation();
            _provider.Register<ITestInterface>(impl);

            // Act
            var result = _provider.TryGet<ITestInterface>(out var retrieved);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(retrieved, Is.SameAs(impl));
        }

        #endregion

        #region Unregister Tests

        [Test]
        public void Unregister_RegisteredType_RemovesIt()
        {
            // Arrange
            var obj = new TestObject { Name = "Test" };
            _provider.Register(obj);

            // Act
            _provider.Unregister<TestObject>();

            // Assert
            var result = _provider.TryGet<TestObject>(out _);
            Assert.That(result, Is.False);
        }

        [Test]
        public void Unregister_UnregisteredType_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _provider.Unregister<TestObject>());
        }

        [Test]
        public void Unregister_ThenRegister_AllowsNewRegistration()
        {
            // Arrange
            var obj1 = new TestObject { Name = "First" };
            var obj2 = new TestObject { Name = "Second" };
            _provider.Register(obj1);
            _provider.Unregister<TestObject>();

            // Act
            _provider.Register(obj2);
            var retrieved = _provider.Get<TestObject>();

            // Assert
            Assert.That(retrieved, Is.SameAs(obj2));
            Assert.That(retrieved.Name, Is.EqualTo("Second"));
        }

        #endregion

        #region Clear Tests

        [Test]
        public void Clear_RemovesAllRegistrations()
        {
            // Arrange
            _provider.Register(new TestObject { Name = "Test" });
            _provider.Register(new AnotherTestObject { Data = 1.0f });
            _provider.Register<ITestInterface>(new TestImplementation());

            // Act
            _provider.Clear();

            // Assert
            Assert.That(_provider.TryGet<TestObject>(out _), Is.False);
            Assert.That(_provider.TryGet<AnotherTestObject>(out _), Is.False);
            Assert.That(_provider.TryGet<ITestInterface>(out _), Is.False);
        }

        [Test]
        public void Clear_EmptyProvider_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _provider.Clear());
        }

        [Test]
        public void Clear_AllowsReregistration()
        {
            // Arrange
            var obj1 = new TestObject { Name = "First" };
            _provider.Register(obj1);
            _provider.Clear();

            // Act
            var obj2 = new TestObject { Name = "Second" };
            _provider.Register(obj2);
            var retrieved = _provider.Get<TestObject>();

            // Assert
            Assert.That(retrieved, Is.SameAs(obj2));
        }

        #endregion

        #region Interface vs Implementation Tests

        [Test]
        public void Register_SameObjectAsDifferentTypes_BothAccessible()
        {
            // Arrange
            var impl = new TestImplementation();
            _provider.Register<ITestInterface>(impl);
            _provider.Register(impl); // Register as concrete type too

            // Act
            var asInterface = _provider.Get<ITestInterface>();
            var asConcrete = _provider.Get<TestImplementation>();

            // Assert
            Assert.That(asInterface, Is.SameAs(impl));
            Assert.That(asConcrete, Is.SameAs(impl));
            Assert.That(asInterface, Is.SameAs(asConcrete));
        }

        #endregion
    }
}
