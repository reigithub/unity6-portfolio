using System.Collections.Generic;
using Game.Shared.Interaction;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Shared
{
    /// <summary>
    /// <see cref="InteractionDetector.SelectNearest"/>（最近接選定の純粋関数）の検証。
    /// </summary>
    [TestFixture]
    public class InteractionDetectorTests
    {
        private static IInteractable Interactable(Vector3 position)
        {
            var mock = Substitute.For<IInteractable>();
            mock.CenterPosition.Returns(position);
            return mock;
        }

        [Test]
        public void SelectNearest_WhenNoCandidates_ReturnsNull()
        {
            // Act
            var result = InteractionDetector.SelectNearest(Vector3.zero, new List<IInteractable>());

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void SelectNearest_WithSingleCandidate_ReturnsThatCandidate()
        {
            // Arrange
            var only = Interactable(new Vector3(3f, 0f, 0f));

            // Act
            var result = InteractionDetector.SelectNearest(Vector3.zero, new List<IInteractable> { only });

            // Assert
            Assert.That(result, Is.SameAs(only));
        }

        [Test]
        public void SelectNearest_WithMultipleCandidates_ReturnsNearest()
        {
            // Arrange
            var far = Interactable(new Vector3(10f, 0f, 0f));
            var near = Interactable(new Vector3(1f, 0f, 0f));
            var mid = Interactable(new Vector3(5f, 0f, 0f));

            // Act
            var result = InteractionDetector.SelectNearest(
                Vector3.zero, new List<IInteractable> { far, near, mid });

            // Assert
            Assert.That(result, Is.SameAs(near));
        }

        [Test]
        public void SelectNearest_MeasuresDistanceFromOrigin()
        {
            // Arrange: origin を b 寄りに置くと b が最近接になる
            var a = Interactable(new Vector3(0f, 0f, 0f));
            var b = Interactable(new Vector3(8f, 0f, 0f));

            // Act
            var result = InteractionDetector.SelectNearest(
                new Vector3(9f, 0f, 0f), new List<IInteractable> { a, b });

            // Assert
            Assert.That(result, Is.SameAs(b));
        }

        [Test]
        public void SelectNearest_WhenTie_ReturnsFirstDeterministically()
        {
            // Arrange: 同じ距離（2）の 2 候補
            var first = Interactable(new Vector3(2f, 0f, 0f));
            var second = Interactable(new Vector3(0f, 2f, 0f));

            // Act
            var result = InteractionDetector.SelectNearest(
                Vector3.zero, new List<IInteractable> { first, second });

            // Assert: 厳密な < で更新するため、同距離なら先頭が残る
            Assert.That(result, Is.SameAs(first));
        }

        [Test]
        public void SelectNearest_SkipsNullEntries()
        {
            // Arrange
            var valid = Interactable(new Vector3(3f, 0f, 0f));
            var candidates = new List<IInteractable> { null, valid, null };

            // Act
            var result = InteractionDetector.SelectNearest(Vector3.zero, candidates);

            // Assert
            Assert.That(result, Is.SameAs(valid));
        }
    }
}
