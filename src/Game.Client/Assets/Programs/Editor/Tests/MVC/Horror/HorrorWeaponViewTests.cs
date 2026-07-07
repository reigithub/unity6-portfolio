using Game.Horror.Player;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.MVC.Horror
{
    [TestFixture]
    public class HorrorWeaponViewTests
    {
        // 下げ量算出：通常は前半 0→1（下げ）/ 後半 1→0（上げ）の三角波。skip 時は 1→0（上げのみ）。

        [Test]
        public void CalculateLowerAmount_AtStart_Normal_IsZero()
            => Assert.That(HorrorWeaponView.CalculateLowerAmount(0f, 1f, false), Is.EqualTo(0f));

        [Test]
        public void CalculateLowerAmount_AtStart_Skip_IsOne()
            => Assert.That(HorrorWeaponView.CalculateLowerAmount(0f, 1f, true), Is.EqualTo(1f));

        [Test]
        public void CalculateLowerAmount_Midway_Normal_IsOne()
            => Assert.That(HorrorWeaponView.CalculateLowerAmount(0.5f, 1f, false), Is.EqualTo(1f).Within(1e-4f));

        [Test]
        public void CalculateLowerAmount_AtEnd_Normal_IsZero()
            => Assert.That(HorrorWeaponView.CalculateLowerAmount(1f, 1f, false), Is.EqualTo(0f).Within(1e-4f));

        [Test]
        public void CalculateLowerAmount_AtEnd_Skip_IsZero()
            => Assert.That(HorrorWeaponView.CalculateLowerAmount(1f, 1f, true), Is.EqualTo(0f).Within(1e-4f));

        // duration<=0 はゼロ除算を避けて 0 とみなす
        [Test]
        public void CalculateLowerAmount_ZeroDuration_IsZero()
            => Assert.That(HorrorWeaponView.CalculateLowerAmount(0.5f, 0f, false), Is.EqualTo(0f));

        [Test]
        public void CalculateLowerAmount_NegativeDuration_IsZero()
            => Assert.That(HorrorWeaponView.CalculateLowerAmount(0.5f, -1f, false), Is.EqualTo(0f));

        // elapsed が duration を超過しても結果は 0-1 にクランプされる
        [Test]
        public void CalculateLowerAmount_PastDuration_IsClamped()
            => Assert.That(HorrorWeaponView.CalculateLowerAmount(2f, 1f, false), Is.EqualTo(0f).Within(1e-4f));

        // モデル入替点：通常は中間点（duration * 0.5）到達で true。skip 時は開始直後（t=0）から true。

        [Test]
        public void IsPastSwapPoint_BeforeMidpoint_Normal_IsFalse()
            => Assert.That(HorrorWeaponView.IsPastSwapPoint(0.4f, 1f, false), Is.False);

        [Test]
        public void IsPastSwapPoint_AtMidpoint_Normal_IsTrue()
            => Assert.That(HorrorWeaponView.IsPastSwapPoint(0.5f, 1f, false), Is.True);

        [Test]
        public void IsPastSwapPoint_AfterMidpoint_Normal_IsTrue()
            => Assert.That(HorrorWeaponView.IsPastSwapPoint(0.6f, 1f, false), Is.True);

        [Test]
        public void IsPastSwapPoint_AtStart_Skip_IsTrue()
            => Assert.That(HorrorWeaponView.IsPastSwapPoint(0f, 1f, true), Is.True);

        // WeaponRoot ローカル位置の算出：基準位置に下げオフセット（lowerAmount）とエイムオフセット（aimBlend）を合成する。

        private static readonly Vector3 BasePosition = new(0f, 0.1f, 0.2f);
        private static readonly Vector3 DownOffset = new(0f, -0.4f, 0f);
        private static readonly Vector3 AimOffset = new(-0.25f, 0.1f, 0f);

        [Test]
        public void CalculateLocalPosition_NoLowerNoBlend_IsBase()
            => Assert.That(HorrorWeaponView.CalculateLocalPosition(BasePosition, DownOffset, 0f, AimOffset, 0f), Is.EqualTo(BasePosition));

        [Test]
        public void CalculateLocalPosition_FullBlend_IsBasePlusAimOffset()
            => Assert.That(HorrorWeaponView.CalculateLocalPosition(BasePosition, DownOffset, 0f, AimOffset, 1f), Is.EqualTo(BasePosition + AimOffset));

        [Test]
        public void CalculateLocalPosition_FullLower_IsBasePlusDownOffset()
            => Assert.That(HorrorWeaponView.CalculateLocalPosition(BasePosition, DownOffset, 1f, AimOffset, 0f), Is.EqualTo(BasePosition + DownOffset));

        [Test]
        public void CalculateLocalPosition_FullLowerAndBlend_IsBasePlusBothOffsets()
            => Assert.That(HorrorWeaponView.CalculateLocalPosition(BasePosition, DownOffset, 1f, AimOffset, 1f), Is.EqualTo(BasePosition + DownOffset + AimOffset));

        // リロード傾き量：開始 transitionSeconds で 0→1（傾け）、終端 transitionSeconds で 1→0（戻し）、間は 1 を保持する台形カーブ。

        [Test]
        public void CalculateReloadTiltWeight_AtStart_IsZero()
            => Assert.That(HorrorWeaponView.CalculateReloadTiltWeight(0f, 3f, 0.4f), Is.EqualTo(0f));

        [Test]
        public void CalculateReloadTiltWeight_DuringTiltTransition_IsHalfway()
            => Assert.That(HorrorWeaponView.CalculateReloadTiltWeight(0.2f, 3f, 0.4f), Is.EqualTo(0.5f).Within(1e-4f));

        [Test]
        public void CalculateReloadTiltWeight_DuringHold_IsOne()
            => Assert.That(HorrorWeaponView.CalculateReloadTiltWeight(1.5f, 3f, 0.4f), Is.EqualTo(1f).Within(1e-4f));

        [Test]
        public void CalculateReloadTiltWeight_AtEnd_IsZero()
            => Assert.That(HorrorWeaponView.CalculateReloadTiltWeight(3f, 3f, 0.4f), Is.EqualTo(0f).Within(1e-4f));

        // duration<=0 はゼロ除算を避けて 0 とみなす
        [Test]
        public void CalculateReloadTiltWeight_ZeroDuration_IsZero()
            => Assert.That(HorrorWeaponView.CalculateReloadTiltWeight(0.5f, 0f, 0.4f), Is.EqualTo(0f));

        // transitionSeconds<=0 は最小値でガードされ、例外なく 0-1 に収まる
        [Test]
        public void CalculateReloadTiltWeight_ZeroTransitionSeconds_IsClampedWithinUnitRange()
        {
            var result = HorrorWeaponView.CalculateReloadTiltWeight(0.5f, 1f, 0f);
            Assert.That(result, Is.InRange(0f, 1f));
        }

        // duration より遷移区間（transitionSeconds * 2）が大きい場合、保持区間なしの三角波化しても 0-1 にクランプされる
        [Test]
        public void CalculateReloadTiltWeight_TransitionLongerThanDuration_IsClamped()
            => Assert.That(HorrorWeaponView.CalculateReloadTiltWeight(0.25f, 0.5f, 0.4f), Is.EqualTo(0.625f).Within(1e-4f));

        // WeaponRoot ローカル回転の算出：基準回転にリロード傾き（ロール角 × 傾き量）を合成する。

        private static readonly Quaternion BaseRotation = Quaternion.Euler(10f, 20f, 30f);
        private const float TiltAngle = -35f;

        [Test]
        public void CalculateLocalRotation_WeightZero_EqualsBaseRotation()
            => Assert.That(Quaternion.Angle(HorrorWeaponView.CalculateLocalRotation(BaseRotation, TiltAngle, 0f), BaseRotation), Is.EqualTo(0f).Within(1e-3f));

        [Test]
        public void CalculateLocalRotation_WeightOne_EqualsBaseRotationTimesTilt()
        {
            var expected = BaseRotation * Quaternion.Euler(0f, 0f, TiltAngle);
            var actual = HorrorWeaponView.CalculateLocalRotation(BaseRotation, TiltAngle, 1f);
            Assert.That(Quaternion.Angle(actual, expected), Is.EqualTo(0f).Within(1e-3f));
        }
    }
}
