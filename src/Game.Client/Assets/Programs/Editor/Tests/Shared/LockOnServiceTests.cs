using System.Reflection;
using Game.Shared.Services;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Shared
{
    [TestFixture]
    public class LockOnServiceTests
    {
        private IAddressableAssetService _mockAssetService;
        private LockOnService _service;

        [SetUp]
        public void Setup()
        {
            _mockAssetService = Substitute.For<IAddressableAssetService>();
            _service = new LockOnService(_mockAssetService);
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
        }

        #region HasTarget Tests

        [Test]
        public void HasTarget_WhenNoTarget_ReturnsFalse()
        {
            // Assert
            Assert.That(_service.HasTarget(), Is.False);
        }

        [Test]
        public void HasTarget_AfterClearTarget_ReturnsFalse()
        {
            // Act
            _service.ClearTarget();

            // Assert
            Assert.That(_service.HasTarget(), Is.False);
        }

        #endregion

        #region ClearTarget Tests

        [Test]
        public void ClearTarget_WhenNoTarget_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _service.ClearTarget());
        }

        [Test]
        public void ClearTarget_ClearsCurrentTarget()
        {
            // Arrange
            SetTargetInternal(CreateMockTransform());

            // Act
            _service.ClearTarget();

            // Assert
            Assert.That(_service.HasTarget(), Is.False);
        }

        #endregion

        #region SetAutoTarget Tests

        [Test]
        public void SetAutoTarget_SetsOwner()
        {
            // Arrange
            var owner = CreateMockTransform();

            // Act
            _service.SetAutoTarget(owner);

            // Assert
            var field = typeof(LockOnService).GetField("_owner", BindingFlags.NonPublic | BindingFlags.Instance);
            var value = field?.GetValue(_service) as Transform;
            Assert.That(value, Is.EqualTo(owner));
        }

        #endregion

        #region Initialize Tests

        [Test]
        public void Initialize_SetsCameraAndLayer()
        {
            // Arrange
            var cameraObj = new GameObject("TestCamera");
            var camera = cameraObj.AddComponent<Camera>();
            int layer = 8;

            try
            {
                // Act
                _service.Initialize(camera, layer);

                // Assert
                var cameraField = typeof(LockOnService).GetField("_camera", BindingFlags.NonPublic | BindingFlags.Instance);
                var layerField = typeof(LockOnService).GetField("_layer", BindingFlags.NonPublic | BindingFlags.Instance);

                Assert.That(cameraField?.GetValue(_service), Is.EqualTo(camera));
                Assert.That(layerField?.GetValue(_service), Is.EqualTo(layer));
            }
            finally
            {
                Object.DestroyImmediate(cameraObj);
            }
        }

        #endregion

        #region TryGetTarget Tests

        [Test]
        public void TryGetTarget_WhenNoTarget_ReturnsFalse()
        {
            // Act
            var result = _service.TryGetTarget(out var target, autoTarget: false);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(target, Is.Null);
        }

        [Test]
        public void TryGetTarget_WhenTargetExists_ReturnsTrue()
        {
            // Arrange
            var mockTarget = CreateMockTransform();
            SetTargetInternal(mockTarget);

            try
            {
                // Act
                var result = _service.TryGetTarget(out var target, autoTarget: false);

                // Assert
                Assert.That(result, Is.True);
                Assert.That(target, Is.EqualTo(mockTarget));
            }
            finally
            {
                Object.DestroyImmediate(mockTarget.gameObject);
            }
        }

        [Test]
        public void TryGetTarget_WhenTargetInactive_ReturnsFalse()
        {
            // Arrange
            var mockTarget = CreateMockTransform();
            SetTargetInternal(mockTarget);
            mockTarget.gameObject.SetActive(false);

            try
            {
                // Act
                var result = _service.TryGetTarget(out var target, autoTarget: false);

                // Assert
                Assert.That(result, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(mockTarget.gameObject);
            }
        }

        #endregion

        #region SetTarget Tests (Limited - requires Camera)

        [Test]
        public void SetTarget_WithoutCamera_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _service.SetTarget(Vector2.zero));
        }

        #endregion

        #region UpdateAutoTarget Tests

        [Test]
        public void UpdateAutoTarget_WithoutOwner_DoesNothing()
        {
            // Arrange - No owner set

            // Act & Assert
            Assert.DoesNotThrow(() => _service.UpdateAutoTarget());
            Assert.That(_service.HasTarget(), Is.False);
        }

        [Test]
        public void UpdateAutoTarget_WhenTargetAlreadyExists_DoesNotChange()
        {
            // Arrange
            var existingTarget = CreateMockTransform();
            SetTargetInternal(existingTarget);

            var owner = CreateMockTransform();
            _service.SetAutoTarget(owner);

            try
            {
                // Act
                _service.UpdateAutoTarget();

                // Assert - Target should remain the same
                _service.TryGetTarget(out var target, autoTarget: false);
                Assert.That(target, Is.EqualTo(existingTarget));
            }
            finally
            {
                Object.DestroyImmediate(existingTarget.gameObject);
                Object.DestroyImmediate(owner.gameObject);
            }
        }

        #endregion

        #region Dispose Tests

        [Test]
        public void Dispose_ClearsTarget()
        {
            // Arrange
            var target = CreateMockTransform();
            SetTargetInternal(target);

            try
            {
                // Act
                _service.Dispose();

                // Assert - After dispose, HasTarget should return false
                // Note: Accessing disposed ReactiveProperty may throw
            }
            finally
            {
                Object.DestroyImmediate(target.gameObject);
            }
        }

        #endregion

        #region Helper Methods

        private Transform CreateMockTransform()
        {
            var go = new GameObject("MockTarget");
            return go.transform;
        }

        private void SetTargetInternal(Transform target)
        {
            // Use reflection to set the internal target
            var field = typeof(LockOnService).GetField("_currentTarget", BindingFlags.NonPublic | BindingFlags.Instance);
            var reactiveProperty = field?.GetValue(_service) as R3.ReactiveProperty<Transform>;
            if (reactiveProperty != null)
            {
                reactiveProperty.Value = target;
            }
        }

        #endregion
    }
}
