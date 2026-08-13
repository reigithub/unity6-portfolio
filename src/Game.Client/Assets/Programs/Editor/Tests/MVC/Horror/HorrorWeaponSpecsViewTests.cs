using Game.Horror.Dialogs;
using NUnit.Framework;

namespace Game.Tests.MVC.Horror
{
    [TestFixture]
    public class HorrorWeaponSpecsViewTests
    {
        // 威力ゲージ値（0〜1）：damage / DamageBest(100)、範囲外はクランプ。

        [Test]
        public void CalculatePowerValue_Zero_IsZero()
            => Assert.That(HorrorWeaponSpecsView.CalculatePowerValue(0), Is.EqualTo(0f));

        [Test]
        public void CalculatePowerValue_AtBest_IsOne()
            => Assert.That(HorrorWeaponSpecsView.CalculatePowerValue(100), Is.EqualTo(1f));

        [Test]
        public void CalculatePowerValue_M1911Damage_ReturnsProportional()
            => Assert.That(HorrorWeaponSpecsView.CalculatePowerValue(30), Is.EqualTo(0.3f).Within(1e-4f));

        [Test]
        public void CalculatePowerValue_OverBest_ClampsToOne()
            => Assert.That(HorrorWeaponSpecsView.CalculatePowerValue(150), Is.EqualTo(1f));

        [Test]
        public void CalculatePowerValue_NegativeDamage_IsZero()
            => Assert.That(HorrorWeaponSpecsView.CalculatePowerValue(-10), Is.EqualTo(0f));

        // 安定性ゲージ値（0〜1）：1 - Clamp01(pitch × recover / InstabilityWorst(5))。

        [Test]
        public void CalculateStabilityValue_NoRecoil_IsOne()
            => Assert.That(HorrorWeaponSpecsView.CalculateStabilityValue(0f, 1f), Is.EqualTo(1f));

        [Test]
        public void CalculateStabilityValue_M1911Recoil_ReturnsExpected()
            => Assert.That(HorrorWeaponSpecsView.CalculateStabilityValue(2.5f, 0.25f), Is.EqualTo(0.875f).Within(1e-4f));

        [Test]
        public void CalculateStabilityValue_AtWorst_IsZero()
            => Assert.That(HorrorWeaponSpecsView.CalculateStabilityValue(10f, 1f), Is.EqualTo(0f));

        [Test]
        public void CalculateStabilityValue_OverWorst_ClampsToZero()
            => Assert.That(HorrorWeaponSpecsView.CalculateStabilityValue(20f, 1f), Is.EqualTo(0f));

        // 射撃精度ゲージ値（0〜1）：1 - Clamp01(spreadAngle / SpreadWorst(10))。

        [Test]
        public void CalculateAccuracyValue_NoSpread_IsOne()
            => Assert.That(HorrorWeaponSpecsView.CalculateAccuracyValue(0f), Is.EqualTo(1f));

        [Test]
        public void CalculateAccuracyValue_M1911Spread_ReturnsExpected()
            => Assert.That(HorrorWeaponSpecsView.CalculateAccuracyValue(2f), Is.EqualTo(0.8f).Within(1e-4f));

        [Test]
        public void CalculateAccuracyValue_AtWorst_IsZero()
            => Assert.That(HorrorWeaponSpecsView.CalculateAccuracyValue(10f), Is.EqualTo(0f));

        [Test]
        public void CalculateAccuracyValue_OverWorst_ClampsToZero()
            => Assert.That(HorrorWeaponSpecsView.CalculateAccuracyValue(15f), Is.EqualTo(0f));

        // 連射速度ゲージ値（0〜1）：Normalize(fireInterval, FireIntervalWorst(2), FireIntervalBest(0.1))。

        [Test]
        public void CalculateFireRateValue_AtBest_IsOne()
            => Assert.That(HorrorWeaponSpecsView.CalculateFireRateValue(0.1f), Is.EqualTo(1f).Within(1e-4f));

        [Test]
        public void CalculateFireRateValue_AtWorst_IsZero()
            => Assert.That(HorrorWeaponSpecsView.CalculateFireRateValue(2f), Is.EqualTo(0f));

        [Test]
        public void CalculateFireRateValue_M1911Interval_ReturnsExpected()
            => Assert.That(HorrorWeaponSpecsView.CalculateFireRateValue(1f), Is.EqualTo(0.526f).Within(1e-3f));

        [Test]
        public void CalculateFireRateValue_BetterThanBest_ClampsToOne()
            => Assert.That(HorrorWeaponSpecsView.CalculateFireRateValue(0f), Is.EqualTo(1f));

        [Test]
        public void CalculateFireRateValue_WorseThanWorst_ClampsToZero()
            => Assert.That(HorrorWeaponSpecsView.CalculateFireRateValue(3f), Is.EqualTo(0f));

        // リロード速度ゲージ値（0〜1）：Normalize(reloadDuration, ReloadWorst(5), ReloadBest(0.5))。

        [Test]
        public void CalculateReloadSpeedValue_AtBest_IsOne()
            => Assert.That(HorrorWeaponSpecsView.CalculateReloadSpeedValue(0.5f), Is.EqualTo(1f).Within(1e-4f));

        [Test]
        public void CalculateReloadSpeedValue_AtWorst_IsZero()
            => Assert.That(HorrorWeaponSpecsView.CalculateReloadSpeedValue(5f), Is.EqualTo(0f));

        [Test]
        public void CalculateReloadSpeedValue_M1911Reload_ReturnsExpected()
            => Assert.That(HorrorWeaponSpecsView.CalculateReloadSpeedValue(3f), Is.EqualTo(0.444f).Within(1e-3f));

        [Test]
        public void CalculateReloadSpeedValue_BetterThanBest_ClampsToOne()
            => Assert.That(HorrorWeaponSpecsView.CalculateReloadSpeedValue(0f), Is.EqualTo(1f));

        // ゼロ除算ガード：worst と best が等しい場合は 0 を返す。

        [Test]
        public void Normalize_WorstEqualsBest_IsZero()
            => Assert.That(HorrorWeaponSpecsView.Normalize(3f, 5f, 5f), Is.EqualTo(0f));
    }
}
