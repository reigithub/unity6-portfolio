using NUnit.Framework;
using UnityEngine;
using Game.Horror.Interaction;

namespace Game.Tests.MVC.Horror.Interaction
{
    /// <summary>
    /// <see cref="InteractionDetector.CalculateAimScore"/> の純粋ロジック検証。
    /// 特に、画面投影を使わないことで近距離（対象がカメラ平面より後ろに回り込む）でも
    /// スコアが反転・破綻しないことを保証する（旧 SphereCast/投影方式の近距離バグ回帰防止）。
    /// </summary>
    [TestFixture]
    public class InteractionDetectorAimTests
    {
        // レティクル ray が bounds を貫けばスコア 0（直撃が最良）
        [Test]
        public void AimScore_RayHitsBounds_ReturnsZero()
        {
            var bounds = new Bounds(Vector3.zero, Vector3.one);
            var ray = new Ray(new Vector3(0f, 0f, -5f), Vector3.forward);

            float score = InteractionDetector.CalculateAimScore(bounds, ray, ray.origin, Vector3.forward, out _);

            Assert.AreEqual(0f, score, 1e-4f);
        }

        // 真横の対象はレティクルを外し、約 90 度になる
        [Test]
        public void AimScore_TargetToTheSide_Returns90Degrees()
        {
            var bounds = new Bounds(new Vector3(5f, 0f, 0f), Vector3.one * 0.2f);
            var ray = new Ray(Vector3.zero, Vector3.forward);

            float score = InteractionDetector.CalculateAimScore(bounds, ray, Vector3.zero, Vector3.forward, out _);

            Assert.AreEqual(90f, score, 1e-3f);
        }

        // カメラ平面より後ろ（深度 z<0）の対象でも反転せず、最大角（≒180度）になる＝近距離回帰しない
        [Test]
        public void AimScore_TargetBehindCamera_DoesNotInvert()
        {
            var bounds = new Bounds(new Vector3(0f, 0f, -5f), Vector3.one * 0.2f);
            var ray = new Ray(Vector3.zero, Vector3.forward);

            float score = InteractionDetector.CalculateAimScore(bounds, ray, Vector3.zero, Vector3.forward, out _);

            Assert.AreEqual(180f, score, 1e-3f);
        }

        // 視界前方でレティクルをわずかに外した対象は小さな角度になる（atan(1/5)≒11.31度）
        [Test]
        public void AimScore_SlightlyOffCenter_ReturnsSmallAngle()
        {
            var bounds = new Bounds(new Vector3(1f, 0f, 5f), Vector3.one * 0.2f);
            var ray = new Ray(Vector3.zero, Vector3.forward);

            float score = InteractionDetector.CalculateAimScore(bounds, ray, Vector3.zero, Vector3.forward, out _);

            Assert.AreEqual(11.3099f, score, 1e-2f);
        }
    }
}
