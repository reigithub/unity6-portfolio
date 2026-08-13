using Game.Horror.WeaponEffect;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.MVC.Horror
{
    /// <summary>
    /// <see cref="HorrorWeaponEffectRegistry"/> の幾何判定（点-球・線分-球）と
    /// 登録・解除・視界倍率合成の検証。
    /// </summary>
    [TestFixture]
    public class HorrorWeaponEffectRegistryTests
    {
        private const float SmokeMultiplier = HorrorSmokeField.SightMultiplier;

        #region 点-球

        [Test]
        public void IsPointInSphere_Inside_IsTrue()
            => Assert.That(HorrorWeaponEffectRegistry.IsPointInSphere(new Vector3(1f, 0f, 0f), Vector3.zero, 2f), Is.True);

        [Test]
        public void IsPointInSphere_OnBoundary_IsTrue()
            => Assert.That(HorrorWeaponEffectRegistry.IsPointInSphere(new Vector3(2f, 0f, 0f), Vector3.zero, 2f), Is.True);

        [Test]
        public void IsPointInSphere_Outside_IsFalse()
            => Assert.That(HorrorWeaponEffectRegistry.IsPointInSphere(new Vector3(2.1f, 0f, 0f), Vector3.zero, 2f), Is.False);

        #endregion

        #region 線分-球

        [Test]
        public void SegmentIntersectsSphere_PassingThrough_IsTrue()
            => Assert.That(
                HorrorWeaponEffectRegistry.SegmentIntersectsSphere(
                    new Vector3(-5f, 0f, 0f), new Vector3(5f, 0f, 0f), Vector3.zero, 1f),
                Is.True);

        [Test]
        public void SegmentIntersectsSphere_Miss_IsFalse()
            => Assert.That(
                HorrorWeaponEffectRegistry.SegmentIntersectsSphere(
                    new Vector3(-5f, 2f, 0f), new Vector3(5f, 2f, 0f), Vector3.zero, 1f),
                Is.False);

        [Test]
        public void SegmentIntersectsSphere_EndpointInside_IsTrue()
            => Assert.That(
                HorrorWeaponEffectRegistry.SegmentIntersectsSphere(
                    new Vector3(0.5f, 0f, 0f), new Vector3(5f, 0f, 0f), Vector3.zero, 1f),
                Is.True);

        [Test]
        public void SegmentIntersectsSphere_Tangent_IsTrue()
            => Assert.That(
                HorrorWeaponEffectRegistry.SegmentIntersectsSphere(
                    new Vector3(-5f, 1f, 0f), new Vector3(5f, 1f, 0f), Vector3.zero, 1f),
                Is.True);

        [Test]
        public void SegmentIntersectsSphere_Degenerate_MatchesPointCheck()
        {
            // from == to の退化は点判定に一致する
            var point = new Vector3(0.5f, 0f, 0f);
            Assert.That(HorrorWeaponEffectRegistry.SegmentIntersectsSphere(point, point, Vector3.zero, 1f), Is.True);

            var outside = new Vector3(3f, 0f, 0f);
            Assert.That(HorrorWeaponEffectRegistry.SegmentIntersectsSphere(outside, outside, Vector3.zero, 1f), Is.False);
        }

        [Test]
        public void SegmentIntersectsSphere_SphereBehindSegment_IsFalse()
        {
            // 無限直線なら交差するが、球が線分の背後にあるため交差しない
            Assert.That(
                HorrorWeaponEffectRegistry.SegmentIntersectsSphere(
                    new Vector3(2f, 0f, 0f), new Vector3(5f, 0f, 0f), Vector3.zero, 1f),
                Is.False);
        }

        #endregion

        #region 登録・解除・視界倍率

        [Test]
        public void GetSightMultiplier_NoEntries_ReturnsOne()
        {
            var registry = new HorrorWeaponEffectRegistry();
            Assert.That(registry.GetSightMultiplier(Vector3.zero, new Vector3(10f, 0f, 0f)), Is.EqualTo(1f));
        }

        [Test]
        public void GetSightMultiplier_TargetInsideSmoke_ReturnsSmokeMultiplier()
        {
            var registry = new HorrorWeaponEffectRegistry();
            registry.Register(1, new Vector3(10f, 0f, 0f), 2f, SmokeMultiplier);

            // ターゲットが煙球の内側（視線は煙の外から）
            Assert.That(
                registry.GetSightMultiplier(Vector3.zero, new Vector3(9f, 0f, 0f)),
                Is.EqualTo(SmokeMultiplier));
        }

        [Test]
        public void GetSightMultiplier_SmokeBetweenEyeAndTarget_ReturnsSmokeMultiplier()
        {
            var registry = new HorrorWeaponEffectRegistry();
            registry.Register(1, new Vector3(5f, 0f, 0f), 1f, SmokeMultiplier);

            // 目・ターゲットとも煙の外だが、視線が煙を貫通する
            Assert.That(
                registry.GetSightMultiplier(Vector3.zero, new Vector3(10f, 0f, 0f)),
                Is.EqualTo(SmokeMultiplier));
        }

        [Test]
        public void GetSightMultiplier_SmokeUnrelated_ReturnsOne()
        {
            var registry = new HorrorWeaponEffectRegistry();
            registry.Register(1, new Vector3(0f, 10f, 0f), 1f, SmokeMultiplier);

            Assert.That(registry.GetSightMultiplier(Vector3.zero, new Vector3(10f, 0f, 0f)), Is.EqualTo(1f));
        }

        [Test]
        public void GetSightMultiplier_MultipleEntries_ReturnsMinimum()
        {
            var registry = new HorrorWeaponEffectRegistry();
            registry.Register(1, new Vector3(3f, 0f, 0f), 1f, 0.5f);
            registry.Register(2, new Vector3(6f, 0f, 0f), 1f, SmokeMultiplier);

            Assert.That(registry.GetSightMultiplier(Vector3.zero, new Vector3(10f, 0f, 0f)), Is.EqualTo(SmokeMultiplier));
        }

        [Test]
        public void Unregister_RemovesEntry()
        {
            var registry = new HorrorWeaponEffectRegistry();
            registry.Register(1, new Vector3(5f, 0f, 0f), 1f, SmokeMultiplier);
            registry.Unregister(1);

            Assert.That(registry.Count, Is.Zero);
            Assert.That(registry.GetSightMultiplier(Vector3.zero, new Vector3(10f, 0f, 0f)), Is.EqualTo(1f));
        }

        [Test]
        public void Unregister_UnknownId_IsIdempotent()
        {
            var registry = new HorrorWeaponEffectRegistry();
            registry.Register(1, Vector3.zero, 1f, SmokeMultiplier);

            registry.Unregister(99);
            registry.Unregister(99);

            Assert.That(registry.Count, Is.EqualTo(1));
        }

        [Test]
        public void Register_SameId_Replaces()
        {
            var registry = new HorrorWeaponEffectRegistry();
            registry.Register(1, new Vector3(0f, 10f, 0f), 1f, SmokeMultiplier);

            // 同一 Id の再登録は置き換え（重複登録を作らない）
            registry.Register(1, new Vector3(5f, 0f, 0f), 1f, SmokeMultiplier);

            Assert.That(registry.Count, Is.EqualTo(1));
            Assert.That(
                registry.GetSightMultiplier(Vector3.zero, new Vector3(10f, 0f, 0f)),
                Is.EqualTo(SmokeMultiplier));
        }

        #endregion
    }
}
