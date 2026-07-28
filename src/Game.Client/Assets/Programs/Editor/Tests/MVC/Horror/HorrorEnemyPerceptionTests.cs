using Game.Core.Services;
using Game.Horror.Enemy;
using Game.Horror.Signals;
using Game.Shared.Scriptable.Database.Tables;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.MVC.Horror
{
    [TestFixture]
    public class HorrorEnemyPerceptionTests
    {
        // 視野錐判定：Dot と cos 閾値の比較（>=）。正面・視野縁の等号境界・視野外・真後ろを検証する。
        // 境界値は独立オラクルとして Mathf.Cos/Sin(angle * Deg2Rad) から toTarget を構築し、
        // Dot(forward, toTarget) が cosHalfAngle とビット一致するようにして境界の >= を確実に検証する。

        [Test]
        public void IsInSightCone_DirectlyAhead_ReturnsTrue()
        {
            var forward = Vector3.forward;
            var cosHalfAngle = Mathf.Cos(60f * Mathf.Deg2Rad);
            Assert.That(HorrorEnemyPerception.IsInSightCone(forward, forward, cosHalfAngle), Is.True);
        }

        [Test]
        public void IsInSightCone_AtConeEdgeBoundary_ReturnsTrue()
        {
            const float halfAngle = 45f;
            var cosHalfAngle = Mathf.Cos(halfAngle * Mathf.Deg2Rad);
            var sinHalfAngle = Mathf.Sin(halfAngle * Mathf.Deg2Rad);
            var forward = Vector3.forward;

            // forward=(0,0,1) との Dot が cosHalfAngle とビット一致するよう構築した境界方向
            var toTargetAtEdge = new Vector3(sinHalfAngle, 0f, cosHalfAngle);

            Assert.That(HorrorEnemyPerception.IsInSightCone(forward, toTargetAtEdge, cosHalfAngle), Is.True);
        }

        [Test]
        public void IsInSightCone_OutsideCone_ReturnsFalse()
        {
            var forward = Vector3.forward;
            var toTargetSide = Vector3.right; // forward との Dot = 0（真横）
            var cosHalfAngle = Mathf.Cos(45f * Mathf.Deg2Rad); // 約 0.707 > 0
            Assert.That(HorrorEnemyPerception.IsInSightCone(forward, toTargetSide, cosHalfAngle), Is.False);
        }

        [Test]
        public void IsInSightCone_DirectlyBehind_ReturnsFalse()
        {
            var forward = Vector3.forward;
            var toTargetBehind = Vector3.back; // Dot = -1
            var cosHalfAngle = Mathf.Cos(60f * Mathf.Deg2Rad);
            Assert.That(HorrorEnemyPerception.IsInSightCone(forward, toTargetBehind, cosHalfAngle), Is.False);
        }

        // 警戒度ゲージ更新：視認中は近いほど速く充填（distance01=0 で 2x、=1 で 1x）、非視認は減衰、
        // 0〜1 にクランプ、dt=0 では不動。

        [Test]
        public void UpdateAwareness_HasSightAtClosestDistance_FillsAtDoubleRate()
        {
            var result = HorrorEnemyPerception.UpdateAwareness(0f, true, 0f, 0.1f, 0.05f, 1f);
            Assert.That(result, Is.EqualTo(0.2f).Within(1e-4f));
        }

        [Test]
        public void UpdateAwareness_HasSightAtFarthestDistance_FillsAtBaseRate()
        {
            var result = HorrorEnemyPerception.UpdateAwareness(0f, true, 1f, 0.1f, 0.05f, 1f);
            Assert.That(result, Is.EqualTo(0.1f).Within(1e-4f));
        }

        [Test]
        public void UpdateAwareness_NoSight_Decays()
        {
            var result = HorrorEnemyPerception.UpdateAwareness(0.5f, false, 0f, 0.1f, 0.2f, 1f);
            Assert.That(result, Is.EqualTo(0.3f).Within(1e-4f));
        }

        [Test]
        public void UpdateAwareness_ClampsToUnitRange()
        {
            var upperClamped = HorrorEnemyPerception.UpdateAwareness(0.95f, true, 0f, 1f, 0f, 1f);
            Assert.That(upperClamped, Is.EqualTo(1f));

            var lowerClamped = HorrorEnemyPerception.UpdateAwareness(0.05f, false, 0f, 0f, 1f, 1f);
            Assert.That(lowerClamped, Is.EqualTo(0f));
        }

        [Test]
        public void UpdateAwareness_ZeroDeltaTime_IsUnchanged()
        {
            var result = HorrorEnemyPerception.UpdateAwareness(0.5f, true, 0f, 0.1f, 0.05f, 0f);
            Assert.That(result, Is.EqualTo(0.5f).Within(1e-4f));
        }

        // 音種分類：Footstep/Gunshot はプレイヤー実位置に相関（プレイヤー知覚位置を更新）、
        // Object（着弾=デコイ可能）/Scream（敵自身の発声）は注意対象位置のみ。

        [Test]
        public void IsPlayerLocatedNoise_Footstep_IsTrue()
            => Assert.That(HorrorEnemyPerception.IsPlayerLocatedNoise(NoiseType.Footstep), Is.True);

        [Test]
        public void IsPlayerLocatedNoise_Gunshot_IsTrue()
            => Assert.That(HorrorEnemyPerception.IsPlayerLocatedNoise(NoiseType.Gunshot), Is.True);

        [Test]
        public void IsPlayerLocatedNoise_Object_IsFalse()
            => Assert.That(HorrorEnemyPerception.IsPlayerLocatedNoise(NoiseType.Object), Is.False);

        [Test]
        public void IsPlayerLocatedNoise_Scream_IsFalse()
            => Assert.That(HorrorEnemyPerception.IsPlayerLocatedNoise(NoiseType.Scream), Is.False);

        // プール再利用：Initialize は前世の警戒度・知覚位置履歴をクリアする
        // （OnDisable は購読解除と視認フラグのクリアのみで、ゲージと位置履歴は残留するため Initialize 側で保証する）。

        [Test]
        public void Initialize_SecondCall_ClearsAwarenessAndPerceivedPositions()
        {
            GameServiceManager.StartUp();
            var messagePipe = new MessagePipeService();
            messagePipe.AddMessageBroker<HorrorSignals.Noise.Occurred>();
            messagePipe.AddMessageBroker<HorrorSignals.Player.Died>();
            messagePipe.Build();
            GameServiceManager.Register<IMessagePipeService, MessagePipeService>(messagePipe);

            var enemyGo = new GameObject("PerceptionReuseTest");
            var targetGo = new GameObject("PerceptionReuseTarget");
            try
            {
                var perception = enemyGo.AddComponent<HorrorEnemyPerception>();
                var master = new HorrorEnemyMaster
                {
                    SightHalfAngle = 60f,
                    HearingRadius = 10f,
                    HearingSensitivity = 1f,
                };
                perception.Initialize(targetGo.transform, master);

                // 足音で警戒度・プレイヤー知覚位置・注意対象位置を汚す（前世の状態を作る）
                messagePipe.Publish(new HorrorSignals.Noise.Occurred(new Vector3(1f, 0f, 0f), 1f, NoiseType.Footstep));
                Assert.That(perception.Awareness, Is.GreaterThan(0f));
                Assert.That(perception.TryGetLastPerceivedPlayerPosition(out _), Is.True);
                Assert.That(perception.TryGetLastNoticedPosition(out _), Is.True);

                perception.Initialize(targetGo.transform, master);

                Assert.That(perception.Awareness, Is.Zero);
                Assert.That(perception.TryGetLastPerceivedPlayerPosition(out _), Is.False);
                Assert.That(perception.TryGetLastNoticedPosition(out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(targetGo);
                Object.DestroyImmediate(enemyGo);
                GameServiceManager.Shutdown();
            }
        }
    }
}
