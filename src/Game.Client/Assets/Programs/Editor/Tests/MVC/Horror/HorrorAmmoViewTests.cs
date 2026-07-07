using Game.Horror.Player;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.MVC.Horror
{
    [TestFixture]
    public class HorrorAmmoViewTests
    {
        // 表示内容の解決：未装備は None、弾薬アイテム設定ありは弾倉/予備、なしは所持数のみ。

        [Test]
        public void ResolveDisplayMode_NoWeapon_IsNone()
            => Assert.That(HorrorAmmoView.ResolveDisplayMode(false, 4), Is.EqualTo(HorrorAmmoView.DisplayMode.None));

        [Test]
        public void ResolveDisplayMode_WeaponWithAmmoItem_IsMagazineAndReserve()
            => Assert.That(HorrorAmmoView.ResolveDisplayMode(true, 4), Is.EqualTo(HorrorAmmoView.DisplayMode.MagazineAndReserve));

        [Test]
        public void ResolveDisplayMode_WeaponWithoutAmmoItem_IsCountOnly()
            => Assert.That(HorrorAmmoView.ResolveDisplayMode(true, 0), Is.EqualTo(HorrorAmmoView.DisplayMode.CountOnly));

        // 目標アルファ：None は常に 0。表示維持中または保持時間内は 1、保持時間超過で 0。

        [Test]
        public void CalculateTargetAlpha_None_IsZeroEvenIfKeepVisible()
            => Assert.That(HorrorAmmoView.CalculateTargetAlpha(HorrorAmmoView.DisplayMode.None, true, 0f, 2f), Is.EqualTo(0f));

        [Test]
        public void CalculateTargetAlpha_KeepVisible_IsOne()
            => Assert.That(HorrorAmmoView.CalculateTargetAlpha(HorrorAmmoView.DisplayMode.CountOnly, true, 999f, 2f), Is.EqualTo(1f));

        [Test]
        public void CalculateTargetAlpha_WithinHoldDuration_IsOne()
            => Assert.That(HorrorAmmoView.CalculateTargetAlpha(HorrorAmmoView.DisplayMode.CountOnly, false, 1f, 2f), Is.EqualTo(1f));

        [Test]
        public void CalculateTargetAlpha_PastHoldDuration_IsZero()
            => Assert.That(HorrorAmmoView.CalculateTargetAlpha(HorrorAmmoView.DisplayMode.CountOnly, false, 2f, 2f), Is.EqualTo(0f));

        // 弾倉側文字色：MagazineAndReserve かつ満タンのみ強調色。それ以外（CountOnly を含む）は通常色。

        private static readonly Color Full = Color.green;
        private static readonly Color Empty = Color.red;
        private static readonly Color Normal = Color.white;

        [Test]
        public void CalculateMagazineColor_FullMagazine_IsFull()
            => Assert.That(HorrorAmmoView.CalculateMagazineColor(HorrorAmmoView.DisplayMode.MagazineAndReserve, 10, 10, Full, Normal), Is.EqualTo(Full));

        [Test]
        public void CalculateMagazineColor_NotFullMagazine_IsNormal()
            => Assert.That(HorrorAmmoView.CalculateMagazineColor(HorrorAmmoView.DisplayMode.MagazineAndReserve, 5, 10, Full, Normal), Is.EqualTo(Normal));

        [Test]
        public void CalculateMagazineColor_ZeroMagazineSize_IsNormal()
            => Assert.That(HorrorAmmoView.CalculateMagazineColor(HorrorAmmoView.DisplayMode.MagazineAndReserve, 0, 0, Full, Normal), Is.EqualTo(Normal));

        [Test]
        public void CalculateMagazineColor_CountOnly_IsAlwaysNormal()
            => Assert.That(HorrorAmmoView.CalculateMagazineColor(HorrorAmmoView.DisplayMode.CountOnly, 10, 10, Full, Normal), Is.EqualTo(Normal));

        // 予備側文字色：0 以下は警告色、正数は通常色。

        [Test]
        public void CalculateReserveColor_Zero_IsEmpty()
            => Assert.That(HorrorAmmoView.CalculateReserveColor(0, Empty, Normal), Is.EqualTo(Empty));

        [Test]
        public void CalculateReserveColor_Negative_IsEmpty()
            => Assert.That(HorrorAmmoView.CalculateReserveColor(-1, Empty, Normal), Is.EqualTo(Empty));

        [Test]
        public void CalculateReserveColor_Positive_IsNormal()
            => Assert.That(HorrorAmmoView.CalculateReserveColor(1, Empty, Normal), Is.EqualTo(Normal));
    }
}
