using System.Collections.Generic;
using Game.Shared.Interaction;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Shared
{
    /// <summary>
    /// <see cref="InteractionDetector.SelectActionable"/>（画面中心に最も近い単一対象の選別）の純粋関数テスト。
    /// frustum・遮蔽・距離の絞り込みは物理シーン依存のため、PlayMode/手動検証に委ねる。
    /// </summary>
    [TestFixture]
    public class InteractionDetectorTests
    {
        private static readonly Vector2 ScreenCenter = new(0.5f, 0.5f);

        private static IInteractable Mock() => Substitute.For<IInteractable>();

        [Test]
        public void SelectActionable_WhenNoCandidates_ReturnsNull()
        {
            var result = InteractionDetector.SelectActionable(
                ScreenCenter, new List<(IInteractable, Vector2)>());

            Assert.That(result, Is.Null);
        }

        [Test]
        public void SelectActionable_WithSingleCandidate_ReturnsThatCandidate()
        {
            var only = Mock();
            var candidates = new List<(IInteractable, Vector2)> { (only, new Vector2(0.2f, 0.2f)) };

            var result = InteractionDetector.SelectActionable(ScreenCenter, candidates);

            Assert.That(result, Is.SameAs(only));
        }

        [Test]
        public void SelectActionable_WithMultipleCandidates_ReturnsNearestToScreenCenter()
        {
            var far = Mock();
            var near = Mock();
            var candidates = new List<(IInteractable, Vector2)>
            {
                (far, new Vector2(0.1f, 0.1f)),   // 画面中心から遠い
                (near, new Vector2(0.55f, 0.5f)), // 画面中心に近い
            };

            var result = InteractionDetector.SelectActionable(ScreenCenter, candidates);

            Assert.That(result, Is.SameAs(near));
        }

        [Test]
        public void SelectActionable_WhenTie_ReturnsFirstDeterministically()
        {
            var first = Mock();
            var second = Mock();
            // 画面中心から左右対称＝等距離。先頭が決定的に返る
            var candidates = new List<(IInteractable, Vector2)>
            {
                (first, new Vector2(0.4f, 0.5f)),
                (second, new Vector2(0.6f, 0.5f)),
            };

            var result = InteractionDetector.SelectActionable(ScreenCenter, candidates);

            Assert.That(result, Is.SameAs(first));
        }

        [Test]
        public void SelectActionable_SkipsNullEntries()
        {
            var valid = Mock();
            var candidates = new List<(IInteractable, Vector2)>
            {
                (null, new Vector2(0.5f, 0.5f)),  // ちょうど中心だが null → スキップ
                (valid, new Vector2(0.3f, 0.3f)),
            };

            var result = InteractionDetector.SelectActionable(ScreenCenter, candidates);

            Assert.That(result, Is.SameAs(valid));
        }
    }
}
