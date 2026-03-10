using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.MVP.Enemy
{
    /// <summary>
    /// SurvivorEnemyView のデッドレコニング補間アルゴリズムを
    /// テスト内で再現して数学的正しさを検証する。
    /// プロダクションコードは MonoBehaviour のため直接テスト不可。
    /// 定数は SurvivorEnemyView と同一値を使用。
    /// </summary>
    [TestFixture]
    public class SurvivorEnemyViewInterpolationTests
    {
        // SurvivorEnemyView と同一定数
        private const float CorrectionDecayRate = 10f;
        private const float MaxCorrectionDistance = 3f;

        #region PredictPosition Tests

        [Test]
        public void PredictPosition_WithVelocity_MovesCorrectly()
        {
            // Arrange
            var lastSync = Vector3.zero;
            var velocity = new Vector3(1f, 0f, 0f);
            float timeSinceSync = 0.5f;

            // Act
            var predicted = lastSync + velocity * timeSinceSync;

            // Assert
            Assert.That(predicted.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(predicted.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(predicted.z, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void PredictPosition_ZeroVelocity_StaysInPlace()
        {
            // Arrange
            var lastSync = new Vector3(5f, 3f, 7f);
            var velocity = Vector3.zero;
            float timeSinceSync = 1.0f;

            // Act
            var predicted = lastSync + velocity * timeSinceSync;

            // Assert
            Assert.That(predicted, Is.EqualTo(lastSync));
        }

        [Test]
        public void PredictPosition_3DMovement_AllAxesApplied()
        {
            // Arrange
            var lastSync = new Vector3(10f, 20f, 30f);
            var velocity = new Vector3(1f, 2f, 3f);
            float timeSinceSync = 0.1f;

            // Act
            var predicted = lastSync + velocity * timeSinceSync;

            // Assert
            Assert.That(predicted.x, Is.EqualTo(10.1f).Within(0.001f));
            Assert.That(predicted.y, Is.EqualTo(20.2f).Within(0.001f));
            Assert.That(predicted.z, Is.EqualTo(30.3f).Within(0.001f));
        }

        #endregion

        #region CorrectionOffset Tests

        [Test]
        public void UpdateProxy_CalculatesCorrectionOffset()
        {
            // Arrange — UpdateProxy のアルゴリズムを再現
            var lastSyncPosition = Vector3.zero;
            var velocity = new Vector3(10f, 0f, 0f);
            float timeSinceSync = 0.1f;
            var correctionOffset = Vector3.zero;

            // 予測位置 = lastSync + velocity * timeSinceSync + correctionOffset
            var predictedPos = lastSyncPosition + velocity * timeSinceSync + correctionOffset;
            // predictedPos = (1, 0, 0)

            var newServerPos = new Vector3(0.8f, 0f, 0f);

            // Act — UpdateProxy のロジック
            var newCorrectionOffset = predictedPos - newServerPos;

            // Assert
            Assert.That(newCorrectionOffset.x, Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(newCorrectionOffset.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(newCorrectionOffset.z, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void UpdateProxy_LargeError_SnapsToServerPosition()
        {
            // Arrange — 補正オフセットが MaxCorrectionDistance を超える
            var correctionOffset = new Vector3(5f, 0f, 0f); // 5 > MaxCorrectionDistance(3)

            // Act — SurvivorEnemyView のスナップ判定
            if (correctionOffset.sqrMagnitude > MaxCorrectionDistance * MaxCorrectionDistance)
            {
                correctionOffset = Vector3.zero;
            }

            // Assert
            Assert.That(correctionOffset, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void UpdateProxy_SmallError_PreservesOffset()
        {
            // Arrange — 補正オフセットが MaxCorrectionDistance 以下
            var correctionOffset = new Vector3(0.5f, 0f, 0f); // 0.5 < MaxCorrectionDistance(3)

            // Act
            if (correctionOffset.sqrMagnitude > MaxCorrectionDistance * MaxCorrectionDistance)
            {
                correctionOffset = Vector3.zero;
            }

            // Assert
            Assert.That(correctionOffset.x, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void UpdateProxy_ExactlyMaxDistance_PreservesOffset()
        {
            // Arrange — 補正オフセットがちょうど MaxCorrectionDistance
            var correctionOffset = new Vector3(3f, 0f, 0f);

            // Act — sqrMagnitude(9) == MaxCorrectionDistance^2(9) → NOT greater than → 保持
            if (correctionOffset.sqrMagnitude > MaxCorrectionDistance * MaxCorrectionDistance)
            {
                correctionOffset = Vector3.zero;
            }

            // Assert
            Assert.That(correctionOffset.x, Is.EqualTo(3f).Within(0.001f));
        }

        #endregion

        #region CorrectionDecay Tests

        [Test]
        public void CorrectionDecay_LargeDt_FullyDecays()
        {
            // Arrange
            var correctionOffset = new Vector3(1f, 0f, 0f);
            float dt = 0.1f;

            // Act — Vector3.Lerp(offset, zero, CorrectionDecayRate * dt)
            // CorrectionDecayRate(10) * 0.1 = 1.0 → Lerp t=1.0 → 完全にゼロ
            var decayed = Vector3.Lerp(correctionOffset, Vector3.zero, CorrectionDecayRate * dt);

            // Assert
            Assert.That(decayed.x, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void CorrectionDecay_SmallDt_PartiallyDecays()
        {
            // Arrange
            var correctionOffset = new Vector3(1f, 0f, 0f);
            float dt = 0.01f;

            // Act — CorrectionDecayRate(10) * 0.01 = 0.1 → Lerp t=0.1
            // Lerp(1, 0, 0.1) = 1 * (1-0.1) + 0 * 0.1 = 0.9
            var decayed = Vector3.Lerp(correctionOffset, Vector3.zero, CorrectionDecayRate * dt);

            // Assert
            Assert.That(decayed.x, Is.EqualTo(0.9f).Within(0.001f));
        }

        [Test]
        public void CorrectionDecay_ZeroOffset_StaysZero()
        {
            // Arrange
            var correctionOffset = Vector3.zero;
            float dt = 0.05f;

            // Act
            var decayed = Vector3.Lerp(correctionOffset, Vector3.zero, CorrectionDecayRate * dt);

            // Assert
            Assert.That(decayed, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void CorrectionDecay_3DOffset_DecaysAllAxes()
        {
            // Arrange
            var correctionOffset = new Vector3(1f, 2f, 3f);
            float dt = 0.02f;

            // Act — CorrectionDecayRate(10) * 0.02 = 0.2 → Lerp t=0.2
            var decayed = Vector3.Lerp(correctionOffset, Vector3.zero, CorrectionDecayRate * dt);

            // Assert — 各軸が 80% に減衰
            Assert.That(decayed.x, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(decayed.y, Is.EqualTo(1.6f).Within(0.001f));
            Assert.That(decayed.z, Is.EqualTo(2.4f).Within(0.001f));
        }

        #endregion

        #region DisplayPosition Tests

        [Test]
        public void DisplayPosition_CombinesPredictionAndCorrection()
        {
            // Arrange
            var lastSync = Vector3.zero;
            var velocity = new Vector3(10f, 0f, 0f);
            float timeSinceSync = 0.5f;
            var correctionOffset = new Vector3(0.1f, 0f, 0f);

            // Act — 表示位置 = 予測位置 + 残余補正
            var predictedPos = lastSync + velocity * timeSinceSync;
            var displayPos = predictedPos + correctionOffset;

            // Assert
            Assert.That(displayPos.x, Is.EqualTo(5.1f).Within(0.001f));
            Assert.That(displayPos.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(displayPos.z, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void DisplayPosition_FullUpdateCycle_ProducesCorrectResult()
        {
            // Arrange — 1フレームの完全な更新サイクルをシミュレート
            var lastSyncPosition = new Vector3(10f, 0f, 0f);
            var velocity = new Vector3(5f, 0f, 0f);
            var correctionOffset = new Vector3(0.5f, 0f, 0f);
            float timeSinceSync = 0.2f;
            float dt = 0.016f; // ~60fps

            // Act — Update() のロジックを再現
            // 1. 予測位置
            var predictedPos = lastSyncPosition + velocity * timeSinceSync;
            // 2. 補正減衰
            correctionOffset = Vector3.Lerp(
                correctionOffset, Vector3.zero, CorrectionDecayRate * dt);
            // 3. 表示位置
            var displayPos = predictedPos + correctionOffset;

            // Assert
            // predicted = (10 + 5*0.2, 0, 0) = (11, 0, 0)
            Assert.That(predictedPos.x, Is.EqualTo(11f).Within(0.001f));
            // correction: Lerp(0.5, 0, 10*0.016) = Lerp(0.5, 0, 0.16) = 0.5*(1-0.16) = 0.42
            Assert.That(correctionOffset.x, Is.EqualTo(0.42f).Within(0.001f));
            // display = 11 + 0.42 = 11.42
            Assert.That(displayPos.x, Is.EqualTo(11.42f).Within(0.001f));
        }

        #endregion
    }
}
