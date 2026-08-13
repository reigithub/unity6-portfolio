using Game.Horror.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.MVC.Horror.Interaction
{
    /// <summary>
    /// <see cref="InteractionDetector.CalculateSurfaceDistance"/> の純関数検証。
    /// 視界内経路の Actionable 距離判定と視界外フォールバックの近接判定が共有する距離定義をここで固定する。
    /// </summary>
    [TestFixture]
    public class InteractionDetectorFallbackTests
    {
        // 中心 (0,0,0)・サイズ 2x2x2 の bounds（表面は各軸 ±1）
        private static readonly Bounds UnitBounds = new(Vector3.zero, new Vector3(2f, 2f, 2f));

        // bounds 内部の点は表面距離 0（ClosestPoint が点自身を返すため）
        [Test]
        public void SurfaceDistance_InsideBounds_ReturnsZero()
        {
            float distance = InteractionDetector.CalculateSurfaceDistance(UnitBounds, new Vector3(0.5f, -0.3f, 0.2f));
            Assert.That(distance, Is.Zero);
        }

        // 表面上の点も距離 0（境界含む）
        [Test]
        public void SurfaceDistance_OnSurface_ReturnsZero()
        {
            float distance = InteractionDetector.CalculateSurfaceDistance(UnitBounds, new Vector3(1f, 0f, 0f));
            Assert.That(distance, Is.Zero);
        }

        // 軸上外部: x=3 は表面 x=1 から 2
        [Test]
        public void SurfaceDistance_OutsideAlongAxis_ReturnsAxisDistance()
        {
            float distance = InteractionDetector.CalculateSurfaceDistance(UnitBounds, new Vector3(3f, 0f, 0f));
            Assert.That(distance, Is.EqualTo(2f).Within(1e-4f));
        }

        // 対角外部: (4,5,0) の最近接点は角 (1,1,0) → 距離 sqrt(3²+4²) = 5
        [Test]
        public void SurfaceDistance_OutsideDiagonal_ReturnsEuclideanDistance()
        {
            float distance = InteractionDetector.CalculateSurfaceDistance(UnitBounds, new Vector3(4f, 5f, 0f));
            Assert.That(distance, Is.EqualTo(5f).Within(1e-4f));
        }

        // ---- IsInForwardHemisphere（前方半面判定。プレイヤー原点・forward=+Z で検証）----

        private static bool IsForward(Vector3 targetPoint, float toleranceDegrees = 0f)
            => InteractionDetector.IsInForwardHemisphere(Vector3.zero, Vector3.forward, targetPoint, toleranceDegrees);

        // 真正面は前方
        [Test]
        public void ForwardHemisphere_DirectlyAhead_ReturnsTrue()
        {
            Assert.That(IsForward(new Vector3(0f, 0f, 2f)), Is.True);
        }

        // 真後ろは後方
        [Test]
        public void ForwardHemisphere_DirectlyBehind_ReturnsFalse()
        {
            Assert.That(IsForward(new Vector3(0f, 0f, -2f)), Is.False);
        }

        // 前方の足元: 縦成分が支配的でも y を無視して前方と判定する（3D 内積では負になるケース）
        [Test]
        public void ForwardHemisphere_AheadBelowFoot_IgnoresVertical_ReturnsTrue()
        {
            Assert.That(IsForward(new Vector3(0f, -1.5f, 0.5f)), Is.True);
        }

        // 後方の足元: y を無視した水平判定で後方
        [Test]
        public void ForwardHemisphere_BehindBelowFoot_ReturnsFalse()
        {
            Assert.That(IsForward(new Vector3(0f, -1.5f, -0.5f)), Is.False);
        }

        // 真横（90° ちょうど）は「180 度以内」に含む（境界含む側の仕様固定）
        [Test]
        public void ForwardHemisphere_ExactlySide_ReturnsTrue()
        {
            Assert.That(IsForward(new Vector3(2f, 0f, 0f)), Is.True);
        }

        // 真横より少し後ろ（約 97°）はマージン 0 で後方
        [Test]
        public void ForwardHemisphere_SlightlyBehindSide_ReturnsFalse()
        {
            Assert.That(IsForward(new Vector3(4f, 0f, -0.5f)), Is.False);
        }

        // 同じ点（約 97°）もヒステリシスのマージン 10° 内なら前方扱いを維持できる
        [Test]
        public void ForwardHemisphere_SlightlyBehindSide_WithinTolerance_ReturnsTrue()
        {
            Assert.That(IsForward(new Vector3(4f, 0f, -0.5f), toleranceDegrees: 10f), Is.True);
        }

        // ほぼ真下（水平成分ゼロ）は前後の区別が無いため前方扱い
        [Test]
        public void ForwardHemisphere_DirectlyBelow_ReturnsTrue()
        {
            Assert.That(IsForward(new Vector3(0f, -1.2f, 0f)), Is.True);
        }
    }
}
