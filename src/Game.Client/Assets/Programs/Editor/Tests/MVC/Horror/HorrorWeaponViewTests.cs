using Game.Horror.Player;
using NUnit.Framework;

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
    }
}
