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
    }
}
