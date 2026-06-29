using Game.Horror.Player;
using NUnit.Framework;

namespace Game.Tests.MVC.Horror
{
    [TestFixture]
    public class HorrorPlayerControllerTests
    {
        // Hold 進捗 = elapsed / holdSeconds。開始・中間・到達・超過と、ゼロ除算ガードを検証する。

        [Test]
        public void CalculateHoldProgress_AtStart_IsZero()
            => Assert.That(HorrorPlayerController.CalculateHoldProgress(0f, 3f), Is.EqualTo(0f));

        [Test]
        public void CalculateHoldProgress_Midway_IsHalf()
            => Assert.That(HorrorPlayerController.CalculateHoldProgress(1.5f, 3f), Is.EqualTo(0.5f).Within(1e-4f));

        [Test]
        public void CalculateHoldProgress_AtThreshold_IsOne()
            => Assert.That(HorrorPlayerController.CalculateHoldProgress(3f, 3f), Is.EqualTo(1f).Within(1e-4f));

        // 到達フレームで僅かに超過しうる生値（表示側で Clamp される前提）
        [Test]
        public void CalculateHoldProgress_PastThreshold_ExceedsOne()
            => Assert.That(HorrorPlayerController.CalculateHoldProgress(4f, 3f), Is.GreaterThan(1f));

        // holdSeconds=0 はゼロ除算を避けて即時完了（1）とみなす
        [Test]
        public void CalculateHoldProgress_ZeroHoldSeconds_IsOne()
            => Assert.That(HorrorPlayerController.CalculateHoldProgress(0f, 0f), Is.EqualTo(1f));

        [Test]
        public void CalculateHoldProgress_NegativeHoldSeconds_IsOne()
            => Assert.That(HorrorPlayerController.CalculateHoldProgress(1f, -2f), Is.EqualTo(1f));
    }
}
