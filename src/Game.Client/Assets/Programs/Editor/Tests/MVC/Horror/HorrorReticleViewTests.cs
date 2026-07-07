using Game.Horror.Player;
using NUnit.Framework;

namespace Game.Tests.MVC.Horror
{
    [TestFixture]
    public class HorrorReticleViewTests
    {
        // セグメント距離：拡散量（spread）と発砲キック量（kick）の合成。

        [Test]
        public void CalculateSegmentDistance_NoSpreadNoKick_IsZero()
            => Assert.That(HorrorReticleView.CalculateSegmentDistance(0f, 24f, 0f, 12f), Is.EqualTo(0f));

        [Test]
        public void CalculateSegmentDistance_FullSpread_IsExpandedDistance()
            => Assert.That(HorrorReticleView.CalculateSegmentDistance(1f, 24f, 0f, 12f), Is.EqualTo(24f));

        [Test]
        public void CalculateSegmentDistance_FullKickOnly_IsKickDistance()
            => Assert.That(HorrorReticleView.CalculateSegmentDistance(0f, 24f, 1f, 12f), Is.EqualTo(12f));

        [Test]
        public void CalculateSegmentDistance_FullSpreadAndKick_IsSum()
            => Assert.That(HorrorReticleView.CalculateSegmentDistance(1f, 24f, 1f, 12f), Is.EqualTo(36f));

        // フェード不透明度：経過時間から 1→0 の比率を算出。fadeSeconds<=0 はゼロ除算を避けて 0。

        [Test]
        public void CalculateFadeAlpha_AtStart_IsOne()
            => Assert.That(HorrorReticleView.CalculateFadeAlpha(0f, 0.5f), Is.EqualTo(1f));

        [Test]
        public void CalculateFadeAlpha_AtFadeSeconds_IsZero()
            => Assert.That(HorrorReticleView.CalculateFadeAlpha(0.5f, 0.5f), Is.EqualTo(0f).Within(1e-4f));

        [Test]
        public void CalculateFadeAlpha_PastFadeSeconds_IsZero()
            => Assert.That(HorrorReticleView.CalculateFadeAlpha(1f, 0.5f), Is.EqualTo(0f));

        [Test]
        public void CalculateFadeAlpha_ZeroFadeSeconds_IsZero()
            => Assert.That(HorrorReticleView.CalculateFadeAlpha(0f, 0f), Is.EqualTo(0f));

        // ドット不透明度：ドット段階なら master、非ドット段階は 0。常時表示オプションは非ドット段階でも 1 にする（Clamp）。

        [Test]
        public void CalculateDotAlpha_DotPhase_ReturnsMaster()
            => Assert.That(HorrorReticleView.CalculateDotAlpha(true, 0.7f, false), Is.EqualTo(0.7f));

        [Test]
        public void CalculateDotAlpha_NotDotPhase_IsZero()
            => Assert.That(HorrorReticleView.CalculateDotAlpha(false, 0.7f, false), Is.EqualTo(0f));

        [Test]
        public void CalculateDotAlpha_NotDotPhase_AlwaysShowDot_IsOne()
            => Assert.That(HorrorReticleView.CalculateDotAlpha(false, 0.7f, true), Is.EqualTo(1f));

        [Test]
        public void CalculateDotAlpha_DotPhase_AlwaysShowDot_IsClampedToOne()
            => Assert.That(HorrorReticleView.CalculateDotAlpha(true, 0.7f, true), Is.EqualTo(1f));

        // セグメント不透明度：セグメント表示段階なら master、非表示段階は kick が下限（発砲キック中は表示段階に依らず見える）。

        [Test]
        public void CalculateSegmentAlpha_SegPhaseActive_ReturnsMaster()
            => Assert.That(HorrorReticleView.CalculateSegmentAlpha(true, 0.7f, 0f), Is.EqualTo(0.7f));

        [Test]
        public void CalculateSegmentAlpha_NotSegPhaseActive_IsZero()
            => Assert.That(HorrorReticleView.CalculateSegmentAlpha(false, 0.7f, 0f), Is.EqualTo(0f));

        [Test]
        public void CalculateSegmentAlpha_NotSegPhaseActive_WithKick_ReturnsKick()
            => Assert.That(HorrorReticleView.CalculateSegmentAlpha(false, 0.7f, 0.5f), Is.EqualTo(0.5f));

        [Test]
        public void CalculateSegmentAlpha_SegPhaseActive_KickExceedsMaster_ReturnsKick()
            => Assert.That(HorrorReticleView.CalculateSegmentAlpha(true, 0.3f, 0.6f), Is.EqualTo(0.6f));
    }
}
