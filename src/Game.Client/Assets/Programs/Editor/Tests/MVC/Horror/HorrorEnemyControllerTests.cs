using Game.Horror.Enemy;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.MVC.Horror
{
    [TestFixture]
    public class HorrorEnemyControllerTests
    {
        // アニメーター Speed 平滑化：指数補間（1-exp(-response・dt)）で target へ追従する。
        // 目標一致・dt=0 は不動、途中値はオーバーシュートなし、反復適用で収束し、dt に依らず結果が一致する。

        [Test]
        public void CalculateSmoothedAnimSpeed_TargetReached_ReturnsTarget()
            => Assert.That(HorrorEnemyController.CalculateSmoothedAnimSpeed(0.5f, 0.5f, 8f, 1f / 60f), Is.EqualTo(0.5f).Within(1e-4f));

        [Test]
        public void CalculateSmoothedAnimSpeed_ZeroDeltaTime_ReturnsCurrent()
            => Assert.That(HorrorEnemyController.CalculateSmoothedAnimSpeed(0.3f, 0.8f, 8f, 0f), Is.EqualTo(0.3f).Within(1e-4f));

        [Test]
        public void CalculateSmoothedAnimSpeed_RisingStep_StaysWithinRange()
        {
            var result = HorrorEnemyController.CalculateSmoothedAnimSpeed(0f, 0.5f, 8f, 1f / 60f);
            Assert.That(result, Is.InRange(0f, 0.5f));
        }

        [Test]
        public void CalculateSmoothedAnimSpeed_RepeatedApplication_ConvergesToTarget()
        {
            var current = 0f;
            const float target = 0.5f;
            for (var i = 0; i < 60; i++)
                current = HorrorEnemyController.CalculateSmoothedAnimSpeed(current, target, 8f, 1f / 60f);

            Assert.That(target - current, Is.LessThan(0.01f));
        }

        [Test]
        public void CalculateSmoothedAnimSpeed_FrameRateIndependent_MatchesAcrossStepSizes()
        {
            var oneStep = HorrorEnemyController.CalculateSmoothedAnimSpeed(0f, 1f, 8f, 0.2f);
            var twoStep = HorrorEnemyController.CalculateSmoothedAnimSpeed(0f, 1f, 8f, 0.1f);
            twoStep = HorrorEnemyController.CalculateSmoothedAnimSpeed(twoStep, 1f, 8f, 0.1f);

            Assert.That(twoStep, Is.EqualTo(oneStep).Within(1e-4f));
        }

        [Test]
        public void CalculateSmoothedAnimSpeed_Decaying_StaysWithinRangeAndConverges()
        {
            var result = HorrorEnemyController.CalculateSmoothedAnimSpeed(0.5f, 0f, 8f, 1f / 60f);
            Assert.That(result, Is.InRange(0f, 0.5f));

            var current = 0.5f;
            const float target = 0f;
            for (var i = 0; i < 60; i++)
                current = HorrorEnemyController.CalculateSmoothedAnimSpeed(current, target, 8f, 1f / 60f);

            Assert.That(current - target, Is.LessThan(0.01f));
        }

        // 視認喪失中の追跡先：LKP（最終目撃位置）を優先し、未視認（zero センチネル）なら聴覚位置へフォールバック。

        [Test]
        public void ResolveLostSightDestination_WithLastKnown_ReturnsLastKnown()
            => Assert.That(
                HorrorEnemyController.ResolveLostSightDestination(new Vector3(5f, 0f, 5f), new Vector3(9f, 0f, 9f)),
                Is.EqualTo(new Vector3(5f, 0f, 5f)));

        [Test]
        public void ResolveLostSightDestination_ZeroLastKnown_ReturnsLastHeard()
            => Assert.That(
                HorrorEnemyController.ResolveLostSightDestination(Vector3.zero, new Vector3(9f, 0f, 9f)),
                Is.EqualTo(new Vector3(9f, 0f, 9f)));
    }
}
