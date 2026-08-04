using NUnit.Framework;
using UnityEngine;
using Game.Horror.Interaction;

namespace Game.Tests.MVC.Horror.Interaction
{
    /// <summary>
    /// <see cref="InteractionDetector.CalculateAimScore"/> の純粋ロジック検証。
    /// 特に、画面投影を使わないことで近距離（対象がカメラ平面より後ろに回り込む）でも
    /// スコアが反転・破綻しないことを保証する（旧 SphereCast/投影方式の近距離バグ回帰防止）。
    /// 直撃の成否は実コライダーへの Raycast（<see cref="InteractionDetector.FindReticleTarget"/>、
    /// 物理依存のため <see cref="InteractionDetectorReticleTargetTests"/> で検証）で決まり、
    /// ここでは直撃フラグを受けた後のスコア・狙い点を固定する。
    /// </summary>
    [TestFixture]
    public class InteractionDetectorAimTests
    {
        // 直撃（レティクル ray が実コライダーにヒット）はスコア 0 が最良、aimPoint は実ヒット点
        [Test]
        public void AimScore_ReticleHit_ReturnsZeroAndHitPoint()
        {
            var bounds = new Bounds(Vector3.zero, Vector3.one);
            var hitPoint = new Vector3(0f, 0f, -0.5f);

            float score = InteractionDetector.CalculateAimScore(
                bounds, isReticleHit: true, hitPoint, new Vector3(0f, 0f, -5f), Vector3.forward, out var aimPoint);

            Assert.AreEqual(0f, score, 1e-4f);
            Assert.That(aimPoint, Is.EqualTo(hitPoint));
        }

        // 真横の対象はレティクルを外し、約 90 度になる
        [Test]
        public void AimScore_TargetToTheSide_Returns90Degrees()
        {
            var bounds = new Bounds(new Vector3(5f, 0f, 0f), Vector3.one * 0.2f);

            float score = InteractionDetector.CalculateAimScore(
                bounds, isReticleHit: false, Vector3.zero, Vector3.zero, Vector3.forward, out _);

            Assert.AreEqual(90f, score, 1e-3f);
        }

        // カメラ平面より後ろ（深度 z<0）の対象でも反転せず、最大角（≒180度）になる＝近距離回帰しない
        [Test]
        public void AimScore_TargetBehindCamera_DoesNotInvert()
        {
            var bounds = new Bounds(new Vector3(0f, 0f, -5f), Vector3.one * 0.2f);

            float score = InteractionDetector.CalculateAimScore(
                bounds, isReticleHit: false, Vector3.zero, Vector3.zero, Vector3.forward, out _);

            Assert.AreEqual(180f, score, 1e-3f);
        }

        // 視界前方でレティクルをわずかに外した対象は小さな角度になる（atan(1/5)≒11.31度）
        [Test]
        public void AimScore_SlightlyOffCenter_ReturnsSmallAngle()
        {
            var bounds = new Bounds(new Vector3(1f, 0f, 5f), Vector3.one * 0.2f);

            float score = InteractionDetector.CalculateAimScore(
                bounds, isReticleHit: false, Vector3.zero, Vector3.zero, Vector3.forward, out _);

            Assert.AreEqual(11.3099f, score, 1e-2f);
        }

        // 非直撃なら、カメラが AABB 内部（開いた扉の合成 AABB 等）でも直撃 0 にならず中心方向の角度になる
        // （旧 AABB 貫通方式で「AABB 内は常時直撃」だった誤直撃の排除を仕様として固定）
        [Test]
        public void AimScore_CameraInsideBoundsWithoutReticleHit_ReturnsCenterAngle()
        {
            // 中心が真横 (5,0,0)・サイズ 20 の AABB はカメラ (0,0,0) を包含する
            var bounds = new Bounds(new Vector3(5f, 0f, 0f), Vector3.one * 20f);

            float score = InteractionDetector.CalculateAimScore(
                bounds, isReticleHit: false, Vector3.zero, Vector3.zero, Vector3.forward, out _);

            Assert.AreEqual(90f, score, 1e-3f);
        }
    }
}
