using Game.Horror.Player;
using NUnit.Framework;

namespace Game.Tests.MVC.Horror
{
    [TestFixture]
    public class HorrorHealthViewTests
    {
        // HP ゲージ値（0〜1）：通常は current/max、max 0 以下はゼロ除算ガードで 0、範囲外はクランプ。

        [Test]
        public void CalculateGaugeValue_Full_IsOne()
            => Assert.That(HorrorHealthView.CalculateGaugeValue(100, 100), Is.EqualTo(1f));

        [Test]
        public void CalculateGaugeValue_Half_IsHalf()
            => Assert.That(HorrorHealthView.CalculateGaugeValue(50, 100), Is.EqualTo(0.5f).Within(1e-4f));

        [Test]
        public void CalculateGaugeValue_Zero_IsZero()
            => Assert.That(HorrorHealthView.CalculateGaugeValue(0, 100), Is.EqualTo(0f));

        [Test]
        public void CalculateGaugeValue_OverMax_ClampsToOne()
            => Assert.That(HorrorHealthView.CalculateGaugeValue(150, 100), Is.EqualTo(1f));

        [Test]
        public void CalculateGaugeValue_ZeroMax_IsZero()
            => Assert.That(HorrorHealthView.CalculateGaugeValue(50, 0), Is.EqualTo(0f));

        [Test]
        public void CalculateGaugeValue_NegativeCurrent_IsZero()
            => Assert.That(HorrorHealthView.CalculateGaugeValue(-10, 100), Is.EqualTo(0f));

        // 目標アルファ：表示維持中または保持時間内は 1、保持時間超過で 0（境界は厳密 < で無敵終了と同じ契約）。

        [Test]
        public void CalculateTargetAlpha_KeepVisible_IsOne()
            => Assert.That(HorrorHealthView.CalculateTargetAlpha(true, 999f, 2f), Is.EqualTo(1f));

        [Test]
        public void CalculateTargetAlpha_WithinHoldDuration_IsOne()
            => Assert.That(HorrorHealthView.CalculateTargetAlpha(false, 1f, 2f), Is.EqualTo(1f));

        [Test]
        public void CalculateTargetAlpha_PastHoldDuration_IsZero()
            => Assert.That(HorrorHealthView.CalculateTargetAlpha(false, 2f, 2f), Is.EqualTo(0f));
    }
}
