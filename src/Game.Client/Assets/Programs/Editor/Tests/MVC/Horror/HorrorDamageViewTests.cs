using Game.Horror.Player;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.MVC.Horror
{
    [TestFixture]
    public class HorrorDamageViewTests
    {
        // スクリーン位置算出：WorldToScreenPoint の結果と上昇オフセットから表示位置を導く。
        // screenPoint.z < 0（カメラ背後）のみ非表示（false）とする。

        [Test]
        public void TryCalculateScreenPosition_BehindCamera_ReturnsFalse()
            => Assert.That(HorrorDamageView.TryCalculateScreenPosition(new Vector3(100f, 200f, -0.1f), 0f, out _), Is.False);

        [Test]
        public void TryCalculateScreenPosition_AtZeroDepth_ReturnsTrue()
            => Assert.That(HorrorDamageView.TryCalculateScreenPosition(new Vector3(100f, 200f, 0f), 0f, out _), Is.True);

        [Test]
        public void TryCalculateScreenPosition_WithRiseOffset_AddsToY()
        {
            HorrorDamageView.TryCalculateScreenPosition(new Vector3(100f, 200f, 10f), 60f, out var position);
            Assert.That(position.y, Is.EqualTo(260f));
        }

        [Test]
        public void TryCalculateScreenPosition_ZeroRiseOffset_KeepsXAndY()
        {
            HorrorDamageView.TryCalculateScreenPosition(new Vector3(100f, 200f, 10f), 0f, out var position);
            Assert.That(position.x, Is.EqualTo(100f));
            Assert.That(position.y, Is.EqualTo(200f));
        }

        [Test]
        public void TryCalculateScreenPosition_AlwaysReturnsZeroDepth()
        {
            HorrorDamageView.TryCalculateScreenPosition(new Vector3(100f, 200f, 10f), 60f, out var position);
            Assert.That(position.z, Is.EqualTo(0f));
        }
    }
}
