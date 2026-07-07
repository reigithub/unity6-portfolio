using Game.Horror.Player;
using NUnit.Framework;
using UnityEngine;

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

        // 装備ショートカットのスロット index 解決：4方向（単軸）→ 0/1/2/3、斜め・閾値未満は -1。
        // スロット並びは 1=左(0) / 2=上(1) / 3=右(2) / 4=下(3)。

        [Test]
        public void ResolveEquipSlotIndex_Left_IsZero()
            => Assert.That(HorrorPlayerController.ResolveEquipSlotIndex(new Vector2(-1f, 0f)), Is.EqualTo(0));

        [Test]
        public void ResolveEquipSlotIndex_Up_IsOne()
            => Assert.That(HorrorPlayerController.ResolveEquipSlotIndex(new Vector2(0f, 1f)), Is.EqualTo(1));

        [Test]
        public void ResolveEquipSlotIndex_Right_IsTwo()
            => Assert.That(HorrorPlayerController.ResolveEquipSlotIndex(new Vector2(1f, 0f)), Is.EqualTo(2));

        [Test]
        public void ResolveEquipSlotIndex_Down_IsThree()
            => Assert.That(HorrorPlayerController.ResolveEquipSlotIndex(new Vector2(0f, -1f)), Is.EqualTo(3));

        // 斜め入力（両軸とも閾値超過）は判定不能として無視する
        [Test]
        public void ResolveEquipSlotIndex_Diagonal_IsNegativeOne()
            => Assert.That(HorrorPlayerController.ResolveEquipSlotIndex(new Vector2(0.707f, 0.707f)), Is.EqualTo(-1));

        [Test]
        public void ResolveEquipSlotIndex_Zero_IsNegativeOne()
            => Assert.That(HorrorPlayerController.ResolveEquipSlotIndex(Vector2.zero), Is.EqualTo(-1));

        [Test]
        public void ResolveEquipSlotIndex_BelowThreshold_IsNegativeOne()
            => Assert.That(HorrorPlayerController.ResolveEquipSlotIndex(new Vector2(0.3f, 0.2f)), Is.EqualTo(-1));

        // 射撃方向の拡散：spreadAngle 0 は forward 不変、180 は randomUnit と一致、中間角は forward との偏差が spreadAngle 以下。

        [Test]
        public void CalculateShotDirection_ZeroSpread_IsForward()
        {
            var forward = Vector3.forward;
            var result = HorrorPlayerController.CalculateShotDirection(forward, Vector3.right, 0f);
            Assert.That(Vector3.Angle(result, forward), Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void CalculateShotDirection_MaxSpread_MatchesRandomUnit()
        {
            var randomUnit = Vector3.right;
            var result = HorrorPlayerController.CalculateShotDirection(Vector3.forward, randomUnit, 180f);
            Assert.That(Vector3.Angle(result, randomUnit), Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void CalculateShotDirection_WithinSpreadAngle_DeviationIsWithinSpread()
        {
            const float spreadAngle = 10f;
            var forward = Vector3.forward;
            var result = HorrorPlayerController.CalculateShotDirection(forward, Vector3.right, spreadAngle);
            Assert.That(Vector3.Angle(result, forward), Is.LessThanOrEqualTo(spreadAngle));
        }

        // エイムダメージ：非エイムは素値、エイムは倍率適用の四捨五入、倍率1.0はエイムでも素値と一致。

        [Test]
        public void CalculateAimedDamage_NotAiming_ReturnsBaseDamage()
            => Assert.That(HorrorPlayerController.CalculateAimedDamage(34, false, 1.2f), Is.EqualTo(34));

        [Test]
        public void CalculateAimedDamage_Aiming_ReturnsRoundedMultipliedDamage()
            => Assert.That(HorrorPlayerController.CalculateAimedDamage(34, true, 1.2f), Is.EqualTo(41));

        [Test]
        public void CalculateAimedDamage_AimingWithUnitMultiplier_ReturnsBaseDamage()
            => Assert.That(HorrorPlayerController.CalculateAimedDamage(34, true, 1.0f), Is.EqualTo(34));
    }
}
