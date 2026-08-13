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

        // 投擲初速：視線方向を right 軸まわりに投げ上げ角ぶん上へ回して初速を乗じる。
        // 視線に対する相対角で回すため、上下を向いても投げ上げ角が保たれる。

        [Test]
        public void CalculateThrowVelocity_ZeroPitch_IsForwardTimesSpeed()
        {
            var result = HorrorPlayerController.CalculateThrowVelocity(Vector3.forward, Vector3.right, 0f, 8f);
            Assert.That(Vector3.Distance(result, Vector3.forward * 8f), Is.LessThan(1e-4f));
        }

        [Test]
        public void CalculateThrowVelocity_PositivePitch_HasUpwardComponent()
        {
            var result = HorrorPlayerController.CalculateThrowVelocity(Vector3.forward, Vector3.right, 20f, 8f);
            Assert.That(result.y, Is.GreaterThan(0f));
            Assert.That(result.z, Is.GreaterThan(0f)); // 前方成分は保たれる
        }

        // 回転のみで速度の大きさは変えない（初速はマスター値そのもの）
        [Test]
        public void CalculateThrowVelocity_PreservesSpeedMagnitude()
        {
            const float speed = 8f;
            var result = HorrorPlayerController.CalculateThrowVelocity(Vector3.forward, Vector3.right, 20f, speed);
            Assert.That(result.magnitude, Is.EqualTo(speed).Within(1e-3f));
        }

        // 仰角20度なら、初速と視線のなす角はちょうど20度
        [Test]
        public void CalculateThrowVelocity_DeviatesFromForwardByPitchAngle()
        {
            const float pitch = 20f;
            var forward = Vector3.forward;
            var result = HorrorPlayerController.CalculateThrowVelocity(forward, Vector3.right, pitch, 8f);
            Assert.That(Vector3.Angle(result, forward), Is.EqualTo(pitch).Within(1e-3f));
        }

        // 見上げた状態でも視線に対する相対角は同じ（ワールド up 基準ではないことの検証）
        [Test]
        public void CalculateThrowVelocity_LookingUpward_KeepsRelativePitch()
        {
            const float pitch = 20f;
            var forward = new Vector3(0f, 1f, 1f).normalized; // 45度見上げ
            var result = HorrorPlayerController.CalculateThrowVelocity(forward, Vector3.right, pitch, 8f);
            Assert.That(Vector3.Angle(result, forward), Is.EqualTo(pitch).Within(1e-3f));
        }

        // 真上を向いた極限でも破綻せず、相対角が保たれる
        [Test]
        public void CalculateThrowVelocity_LookingStraightUp_KeepsRelativePitch()
        {
            const float pitch = 20f;
            var forward = Vector3.up;
            var result = HorrorPlayerController.CalculateThrowVelocity(forward, Vector3.right, pitch, 8f);
            Assert.That(Vector3.Angle(result, forward), Is.EqualTo(pitch).Within(1e-3f));
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

        // リロード装填数：弾倉不足分と予備所持数の小さい方。満タン・予備0は0、異常値（弾倉>容量）でも負にならない。

        [Test]
        public void CalculateReloadAmount_EmptyMagazineWithEnoughReserve_ReturnsMagazineSize()
            => Assert.That(HorrorPlayerController.CalculateReloadAmount(0, 10, 100), Is.EqualTo(10));

        [Test]
        public void CalculateReloadAmount_PartialMagazine_ReturnsShortfall()
            => Assert.That(HorrorPlayerController.CalculateReloadAmount(4, 10, 100), Is.EqualTo(6));

        [Test]
        public void CalculateReloadAmount_InsufficientReserve_ReturnsReserveAmount()
            => Assert.That(HorrorPlayerController.CalculateReloadAmount(4, 10, 3), Is.EqualTo(3));

        [Test]
        public void CalculateReloadAmount_FullMagazine_ReturnsZero()
            => Assert.That(HorrorPlayerController.CalculateReloadAmount(10, 10, 100), Is.EqualTo(0));

        [Test]
        public void CalculateReloadAmount_ZeroReserve_ReturnsZero()
            => Assert.That(HorrorPlayerController.CalculateReloadAmount(4, 10, 0), Is.EqualTo(0));

        [Test]
        public void CalculateReloadAmount_MagazineExceedsSize_DoesNotGoNegative()
            => Assert.That(HorrorPlayerController.CalculateReloadAmount(15, 10, 100), Is.EqualTo(0));

        // カメラリコイル表示 pitch：pitch - recoilPitch * weight（±89° クランプ込み）。
        // weight 0 は無補正、weight 1 は満額減算、中間 weight は按分、クランプは両端で効く。

        [Test]
        public void CalculateRecoiledPitch_ZeroWeight_ReturnsPitch()
            => Assert.That(HorrorPlayerController.CalculateRecoiledPitch(10f, 2.5f, 0f), Is.EqualTo(10f).Within(1e-4f));

        [Test]
        public void CalculateRecoiledPitch_FullWeight_SubtractsRecoilPitch()
            => Assert.That(HorrorPlayerController.CalculateRecoiledPitch(10f, 2.5f, 1f), Is.EqualTo(7.5f).Within(1e-4f));

        [Test]
        public void CalculateRecoiledPitch_DecayedWeight_SubtractsScaledRecoil()
            => Assert.That(HorrorPlayerController.CalculateRecoiledPitch(10f, 2.5f, 0.5f), Is.EqualTo(8.75f).Within(1e-4f));

        [Test]
        public void CalculateRecoiledPitch_BeyondUpperLimit_ClampsToNegative89()
            => Assert.That(HorrorPlayerController.CalculateRecoiledPitch(-88f, 5f, 1f), Is.EqualTo(-89f).Within(1e-4f));

        [Test]
        public void CalculateRecoiledPitch_BeyondLowerLimit_ClampsTo89()
            => Assert.That(HorrorPlayerController.CalculateRecoiledPitch(88f, -5f, 1f), Is.EqualTo(89f).Within(1e-4f));

        // 足音の歩幅積算ステップ：stride 到達で発火し超過分のみ持ち越す。複数歩分を1フレームで
        // 移動しても発火は1回・剰余は [0, stride) に収まる。stride 0 以下は無限発火防止で発火せず 0 固定。

        [Test]
        public void StepFootstep_BelowStride_DoesNotFireAndAccumulates()
        {
            var (fired, next) = HorrorPlayerController.StepFootstep(0.5f, 0.25f, 1.25f);
            Assert.That(fired, Is.False);
            Assert.That(next, Is.EqualTo(0.75f).Within(1e-4f));
        }

        [Test]
        public void StepFootstep_ExactlyAtStride_FiresAndResetsToZero()
        {
            var (fired, next) = HorrorPlayerController.StepFootstep(0.75f, 0.5f, 1.25f);
            Assert.That(fired, Is.True);
            Assert.That(next, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void StepFootstep_ExceedsStride_FiresAndKeepsRemainder()
        {
            var (fired, next) = HorrorPlayerController.StepFootstep(1.0f, 0.5f, 1.25f);
            Assert.That(fired, Is.True);
            Assert.That(next, Is.EqualTo(0.25f).Within(1e-4f));
        }

        [Test]
        public void StepFootstep_MultipleStridesInOneFrame_FiresOnceAndRemainderStaysBelowStride()
        {
            var (fired, next) = HorrorPlayerController.StepFootstep(0f, 2.6f, 1.25f);
            Assert.That(fired, Is.True);
            Assert.That(next, Is.EqualTo(0.1f).Within(1e-4f));
        }

        [Test]
        public void StepFootstep_NoMovement_DoesNotFireAndUnchanged()
        {
            var (fired, next) = HorrorPlayerController.StepFootstep(0.6f, 0f, 1.25f);
            Assert.That(fired, Is.False);
            Assert.That(next, Is.EqualTo(0.6f).Within(1e-4f));
        }

        [Test]
        public void StepFootstep_ZeroStride_DoesNotFireAndReturnsZero()
        {
            var (fired, next) = HorrorPlayerController.StepFootstep(0.5f, 10f, 0f);
            Assert.That(fired, Is.False);
            Assert.That(next, Is.EqualTo(0f));
        }

        [Test]
        public void StepFootstep_NegativeStride_DoesNotFireAndReturnsZero()
        {
            var (fired, next) = HorrorPlayerController.StepFootstep(0.5f, 10f, -1f);
            Assert.That(fired, Is.False);
            Assert.That(next, Is.EqualTo(0f));
        }

        // 足音 Loudness：走りは走り値、歩き（エイム歩行含む）は歩き値。しゃがみ無音は UpdateFootstep 側のガードで保証。

        [Test]
        public void CalculateFootstepLoudness_Running_ReturnsRunLoudness()
            => Assert.That(HorrorPlayerController.CalculateFootstepLoudness(true, 0.5f, 1f), Is.EqualTo(1f));

        [Test]
        public void CalculateFootstepLoudness_Walking_ReturnsWalkLoudness()
            => Assert.That(HorrorPlayerController.CalculateFootstepLoudness(false, 0.5f, 1f), Is.EqualTo(0.5f));

        // 被弾後の無敵時間判定：窓内は無敵、境界（経過==秒数）で無敵終了。未被弾（負の無限大）と秒数 0 以下は常に無敵なし。

        [Test]
        public void IsInvincible_WithinWindow_IsTrue()
            => Assert.That(HorrorPlayerController.IsInvincible(1.0f, 0.7f, 0.5f), Is.True);

        [Test]
        public void IsInvincible_AtExactBoundary_IsFalse()
            => Assert.That(HorrorPlayerController.IsInvincible(1.2f, 0.7f, 0.5f), Is.False);

        [Test]
        public void IsInvincible_AfterWindow_IsFalse()
            => Assert.That(HorrorPlayerController.IsInvincible(2.0f, 0.7f, 0.5f), Is.False);

        [Test]
        public void IsInvincible_NeverDamaged_NegativeInfinity_IsFalse()
            => Assert.That(HorrorPlayerController.IsInvincible(0f, float.NegativeInfinity, 0.5f), Is.False);

        [Test]
        public void IsInvincible_SameFrameAsDamage_IsTrue()
            => Assert.That(HorrorPlayerController.IsInvincible(1.0f, 1.0f, 0.5f), Is.True);

        [Test]
        public void IsInvincible_ZeroSeconds_IsFalse()
            => Assert.That(HorrorPlayerController.IsInvincible(1.0f, 1.0f, 0f), Is.False);

        // 被弾後の残 HP：通常減算、致死ちょうど・過剰ダメージは 0 で止まる（負にならない）。

        [Test]
        public void CalculateDamagedHealth_Normal_Subtracts()
            => Assert.That(HorrorPlayerController.CalculateDamagedHealth(100, 10), Is.EqualTo(90));

        [Test]
        public void CalculateDamagedHealth_ExactKill_IsZero()
            => Assert.That(HorrorPlayerController.CalculateDamagedHealth(10, 10), Is.EqualTo(0));

        [Test]
        public void CalculateDamagedHealth_Overkill_ClampsToZero()
            => Assert.That(HorrorPlayerController.CalculateDamagedHealth(5, 999), Is.EqualTo(0));

        [Test]
        public void CalculateDamagedHealth_ZeroDamage_Unchanged()
            => Assert.That(HorrorPlayerController.CalculateDamagedHealth(50, 0), Is.EqualTo(50));

        // ロード HP の正規化：0 以下（旧セーブ既定値・新規・不正値）は Max へ、Max 超はクランプ、正常値はそのまま。

        [Test]
        public void NormalizeLoadedHealth_Zero_ReturnsMax()
            => Assert.That(HorrorPlayerController.NormalizeLoadedHealth(0, 100), Is.EqualTo(100));

        [Test]
        public void NormalizeLoadedHealth_Negative_ReturnsMax()
            => Assert.That(HorrorPlayerController.NormalizeLoadedHealth(-5, 100), Is.EqualTo(100));

        [Test]
        public void NormalizeLoadedHealth_Normal_ReturnsSaved()
            => Assert.That(HorrorPlayerController.NormalizeLoadedHealth(40, 100), Is.EqualTo(40));

        [Test]
        public void NormalizeLoadedHealth_AboveMax_ClampsToMax()
            => Assert.That(HorrorPlayerController.NormalizeLoadedHealth(150, 100), Is.EqualTo(100));

        [Test]
        public void NormalizeLoadedHealth_AtMax_ReturnsMax()
            => Assert.That(HorrorPlayerController.NormalizeLoadedHealth(100, 100), Is.EqualTo(100));

        // 回復後の残 HP：通常加算、最大値ちょうど・超過はクランプ（超えない）。

        [Test]
        public void CalculateHealedHealth_Normal_Adds()
            => Assert.That(HorrorPlayerController.CalculateHealedHealth(50, 10, 100), Is.EqualTo(60));

        [Test]
        public void CalculateHealedHealth_AtMax_StaysMax()
            => Assert.That(HorrorPlayerController.CalculateHealedHealth(100, 10, 100), Is.EqualTo(100));

        [Test]
        public void CalculateHealedHealth_OverMax_ClampsToMax()
            => Assert.That(HorrorPlayerController.CalculateHealedHealth(95, 25, 100), Is.EqualTo(100));

        [Test]
        public void CalculateHealedHealth_ZeroAmount_Unchanged()
            => Assert.That(HorrorPlayerController.CalculateHealedHealth(50, 0, 100), Is.EqualTo(50));

        // アイテム使用の適用済み回復総量：経過比率からの再計算。開始 0 → 全量へ単調に増え、
        // 期間超過はクランプ。duration 0 以下は即全量（ゼロ除算ガード）。丸め境界 .5 は避けて検証。

        [Test]
        public void CalculateAppliedHeal_AtStart_IsZero()
            => Assert.That(HorrorPlayerController.CalculateAppliedHeal(25, 0f, 2f), Is.EqualTo(0));

        [Test]
        public void CalculateAppliedHeal_Midway_IsProportional()
            => Assert.That(HorrorPlayerController.CalculateAppliedHeal(25, 0.8f, 2f), Is.EqualTo(10));

        [Test]
        public void CalculateAppliedHeal_AtDuration_IsFullEffect()
            => Assert.That(HorrorPlayerController.CalculateAppliedHeal(25, 2f, 2f), Is.EqualTo(25));

        [Test]
        public void CalculateAppliedHeal_PastDuration_ClampsToEffect()
            => Assert.That(HorrorPlayerController.CalculateAppliedHeal(25, 5f, 2f), Is.EqualTo(25));

        [Test]
        public void CalculateAppliedHeal_ZeroDuration_IsFullEffect()
            => Assert.That(HorrorPlayerController.CalculateAppliedHeal(25, 0f, 0f), Is.EqualTo(25));

        [Test]
        public void CalculateAppliedHeal_NegativeDuration_IsFullEffect()
            => Assert.That(HorrorPlayerController.CalculateAppliedHeal(25, 0f, -1f), Is.EqualTo(25));

        [Test]
        public void CalculateAppliedHeal_NegativeElapsed_IsZero()
            => Assert.That(HorrorPlayerController.CalculateAppliedHeal(25, -0.5f, 2f), Is.EqualTo(0));

        [Test]
        public void CalculateAppliedHeal_MonotonicOverTime()
        {
            var early = HorrorPlayerController.CalculateAppliedHeal(25, 0.4f, 2f);
            var middle = HorrorPlayerController.CalculateAppliedHeal(25, 1.2f, 2f);
            var late = HorrorPlayerController.CalculateAppliedHeal(25, 1.9f, 2f);

            Assert.That(middle, Is.GreaterThanOrEqualTo(early));
            Assert.That(late, Is.GreaterThanOrEqualTo(middle));
        }
    }
}
