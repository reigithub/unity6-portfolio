using System.Reflection;
using Game.ScoreTimeAttack.Data;
using Game.ScoreTimeAttack.Enums;
using Game.ScoreTimeAttack.Scenes;
using NUnit.Framework;

namespace Game.Tests.MVC
{
    [TestFixture]
    public class ScoreTimeAttackModelTests
    {
        #region ScoreTimeAttackStageResultData Tests

        [TestFixture]
        public class StageResultDataTests
        {
            [Test]
            public void GetRemainingTime_ReturnsCorrectValue()
            {
                // Arrange
                var result = new ScoreTimeAttackStageResultData
                {
                    TotalTime = 60,
                    CurrentTime = 45
                };

                // Act
                var remainingTime = result.GetRemainingTime();

                // Assert
                // TotalTime - |CurrentTime - TotalTime| = 60 - |45 - 60| = 60 - 15 = 45
                Assert.That(remainingTime, Is.EqualTo(45));
            }

            [Test]
            public void GetRemainingTime_WhenTimeUp_ReturnsZero()
            {
                // Arrange
                var result = new ScoreTimeAttackStageResultData
                {
                    TotalTime = 60,
                    CurrentTime = 0
                };

                // Act
                var remainingTime = result.GetRemainingTime();

                // Assert
                // TotalTime - |CurrentTime - TotalTime| = 60 - |0 - 60| = 60 - 60 = 0
                Assert.That(remainingTime, Is.EqualTo(0));
            }

            [Test]
            public void GetRemainingTime_WhenFullTimeRemaining_ReturnsTotalTime()
            {
                // Arrange
                var result = new ScoreTimeAttackStageResultData
                {
                    TotalTime = 60,
                    CurrentTime = 60
                };

                // Act
                var remainingTime = result.GetRemainingTime();

                // Assert
                // TotalTime - |CurrentTime - TotalTime| = 60 - |60 - 60| = 60 - 0 = 60
                Assert.That(remainingTime, Is.EqualTo(60));
            }

            [Test]
            public void CalculateScore_ReturnsCorrectValue()
            {
                // Arrange
                var result = new ScoreTimeAttackStageResultData
                {
                    TotalTime = 60,
                    CurrentTime = 40, // remainingTime = 40
                    CurrentPoint = 500,
                    PlayerCurrentHp = 100
                };

                // Act
                var score = result.CalculateScore();

                // Assert
                // remainingTime * CurrentPoint * PlayerCurrentHp = 40 * 500 * 100 = 2,000,000
                Assert.That(score, Is.EqualTo(2_000_000));
            }

            [Test]
            public void CalculateScore_WhenZeroHp_ReturnsZero()
            {
                // Arrange
                var result = new ScoreTimeAttackStageResultData
                {
                    TotalTime = 60,
                    CurrentTime = 40,
                    CurrentPoint = 500,
                    PlayerCurrentHp = 0
                };

                // Act
                var score = result.CalculateScore();

                // Assert
                Assert.That(score, Is.EqualTo(0));
            }

            [Test]
            public void CalculateScore_WhenZeroPoint_ReturnsZero()
            {
                // Arrange
                var result = new ScoreTimeAttackStageResultData
                {
                    TotalTime = 60,
                    CurrentTime = 40,
                    CurrentPoint = 0,
                    PlayerCurrentHp = 100
                };

                // Act
                var score = result.CalculateScore();

                // Assert
                Assert.That(score, Is.EqualTo(0));
            }

            [Test]
            public void CalculateScore_WhenTimeUp_ReturnsZero()
            {
                // Arrange
                var result = new ScoreTimeAttackStageResultData
                {
                    TotalTime = 60,
                    CurrentTime = 0, // remainingTime = 0
                    CurrentPoint = 500,
                    PlayerCurrentHp = 100
                };

                // Act
                var score = result.CalculateScore();

                // Assert
                Assert.That(score, Is.EqualTo(0));
            }
        }

        #endregion

        #region ScoreTimeAttackStageTotalResultData Tests

        [TestFixture]
        public class StageTotalResultDataTests
        {
            [Test]
            public void CalculateTotalScore_SingleStage_ReturnsCorrectValue()
            {
                // Arrange
                var stageResult = new ScoreTimeAttackStageResultData
                {
                    TotalTime = 60,
                    CurrentTime = 50, // remainingTime = 50
                    CurrentPoint = 100,
                    PlayerCurrentHp = 80
                };
                var totalResult = new ScoreTimeAttackStageTotalResultData
                {
                    StageResults = new[] { stageResult }
                };

                // Act
                var totalScore = totalResult.CalculateTotalScore();

                // Assert
                // remainingTime * totalPoint * totalHp = 50 * 100 * 80 = 400,000
                Assert.That(totalScore, Is.EqualTo(400_000));
            }

            [Test]
            public void CalculateTotalScore_MultipleStages_SumsAllValues()
            {
                // Arrange
                var stage1 = new ScoreTimeAttackStageResultData
                {
                    TotalTime = 60,
                    CurrentTime = 40, // remainingTime = 40
                    CurrentPoint = 100,
                    PlayerCurrentHp = 80
                };
                var stage2 = new ScoreTimeAttackStageResultData
                {
                    TotalTime = 60,
                    CurrentTime = 30, // remainingTime = 30
                    CurrentPoint = 200,
                    PlayerCurrentHp = 60
                };
                var stage3 = new ScoreTimeAttackStageResultData
                {
                    TotalTime = 60,
                    CurrentTime = 50, // remainingTime = 50
                    CurrentPoint = 150,
                    PlayerCurrentHp = 100
                };
                var totalResult = new ScoreTimeAttackStageTotalResultData
                {
                    StageResults = new[] { stage1, stage2, stage3 }
                };

                // Act
                var totalScore = totalResult.CalculateTotalScore();

                // Assert
                // totalRemainingTime = 40 + 30 + 50 = 120
                // totalPoint = 100 + 200 + 150 = 450
                // totalHp = 80 + 60 + 100 = 240
                // totalScore = 120 * 450 * 240 = 12,960,000
                Assert.That(totalScore, Is.EqualTo(12_960_000));
            }

            [Test]
            public void CalculateTotalScore_EmptyResults_ReturnsZero()
            {
                // Arrange
                var totalResult = new ScoreTimeAttackStageTotalResultData
                {
                    StageResults = new ScoreTimeAttackStageResultData[0]
                };

                // Act
                var totalScore = totalResult.CalculateTotalScore();

                // Assert
                Assert.That(totalScore, Is.EqualTo(0));
            }
        }

        #endregion

        #region ScoreTimeAttackStageSceneModel Tests

        [TestFixture]
        public class StageSceneModelTests
        {
            private ScoreTimeAttackStageSceneModel _model;

            [SetUp]
            public void Setup()
            {
                _model = new ScoreTimeAttackStageSceneModel();
            }

            private void SetupModelState(int currentTime, int totalTime, int currentPoint, int maxPoint, int currentHp, int maxHp)
            {
                _model.CurrentTime.Value = currentTime;
                SetPrivateField(_model, "TotalTime", totalTime);
                _model.CurrentPoint.Value = currentPoint;
                SetPrivateField(_model, "MaxPoint", maxPoint);
                SetPrivateField(_model, "PlayerCurrentHp", currentHp);
                SetPrivateField(_model, "PlayerMaxHp", maxHp);
            }

            private void SetPrivateField(object obj, string fieldName, object value)
            {
                var prop = obj.GetType().GetProperty(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(obj, value);
                    return;
                }

                var field = obj.GetType().GetField($"<{fieldName}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                field?.SetValue(obj, value);
            }

            #region ProgressTime Tests

            [Test]
            public void ProgressTime_DecrementsCurrentTime()
            {
                // Arrange
                _model.CurrentTime.Value = 60;

                // Act
                _model.ProgressTime();

                // Assert
                Assert.That(_model.CurrentTime.Value, Is.EqualTo(59));
            }

            [Test]
            public void ProgressTime_WhenZero_DoesNotGoNegative()
            {
                // Arrange
                _model.CurrentTime.Value = 0;

                // Act
                _model.ProgressTime();

                // Assert
                Assert.That(_model.CurrentTime.Value, Is.EqualTo(0));
            }

            [Test]
            public void ProgressTime_MultipleCallsDecrementCorrectly()
            {
                // Arrange
                _model.CurrentTime.Value = 5;

                // Act
                for (int i = 0; i < 10; i++)
                {
                    _model.ProgressTime();
                }

                // Assert
                Assert.That(_model.CurrentTime.Value, Is.EqualTo(0));
            }

            #endregion

            #region AddPoint Tests

            [Test]
            public void AddPoint_AddsPoints()
            {
                // Arrange
                SetupModelState(60, 60, 0, 1000, 100, 100);

                // Act
                _model.AddPoint(100);

                // Assert
                Assert.That(_model.CurrentPoint.Value, Is.EqualTo(100));
            }

            [Test]
            public void AddPoint_ClampsToMaxPoint()
            {
                // Arrange
                SetupModelState(60, 60, 900, 1000, 100, 100);

                // Act
                _model.AddPoint(200);

                // Assert
                Assert.That(_model.CurrentPoint.Value, Is.EqualTo(1000));
            }

            [Test]
            public void AddPoint_DoesNotGoNegative()
            {
                // Arrange
                SetupModelState(60, 60, 50, 1000, 100, 100);

                // Act
                _model.AddPoint(-100);

                // Assert
                Assert.That(_model.CurrentPoint.Value, Is.EqualTo(0));
            }

            #endregion

            #region PlayerHpDamaged Tests

            [Test]
            public void PlayerHpDamaged_ReducesHp()
            {
                // Arrange
                SetupModelState(60, 60, 0, 1000, 100, 100);

                // Act
                _model.PlayerHpDamaged(30);

                // Assert
                var currentHp = (int)_model.GetType().GetProperty("PlayerCurrentHp").GetValue(_model);
                Assert.That(currentHp, Is.EqualTo(70));
            }

            [Test]
            public void PlayerHpDamaged_ClampsToZero()
            {
                // Arrange
                SetupModelState(60, 60, 0, 1000, 50, 100);

                // Act
                _model.PlayerHpDamaged(100);

                // Assert
                var currentHp = (int)_model.GetType().GetProperty("PlayerCurrentHp").GetValue(_model);
                Assert.That(currentHp, Is.EqualTo(0));
            }

            [Test]
            public void PlayerHpDamaged_MultipleDamages()
            {
                // Arrange
                SetupModelState(60, 60, 0, 1000, 100, 100);

                // Act
                _model.PlayerHpDamaged(20);
                _model.PlayerHpDamaged(30);
                _model.PlayerHpDamaged(10);

                // Assert
                var currentHp = (int)_model.GetType().GetProperty("PlayerCurrentHp").GetValue(_model);
                Assert.That(currentHp, Is.EqualTo(40));
            }

            #endregion

            #region IsTimeUp Tests

            [Test]
            public void IsTimeUp_WhenTimeZero_ReturnsTrue()
            {
                // Arrange
                _model.CurrentTime.Value = 0;

                // Act & Assert
                Assert.That(_model.IsTimeUp(), Is.True);
            }

            [Test]
            public void IsTimeUp_WhenTimeRemaining_ReturnsFalse()
            {
                // Arrange
                _model.CurrentTime.Value = 1;

                // Act & Assert
                Assert.That(_model.IsTimeUp(), Is.False);
            }

            #endregion

            #region IsClear Tests

            [Test]
            public void IsClear_WhenPointsReachMax_ReturnsTrue()
            {
                // Arrange
                SetupModelState(60, 60, 1000, 1000, 100, 100);

                // Act & Assert
                Assert.That(_model.IsClear(), Is.True);
            }

            [Test]
            public void IsClear_WhenPointsExceedMax_ReturnsTrue()
            {
                // Arrange
                SetupModelState(60, 60, 1100, 1000, 100, 100);

                // Act & Assert
                Assert.That(_model.IsClear(), Is.True);
            }

            [Test]
            public void IsClear_WhenPointsBelowMax_ReturnsFalse()
            {
                // Arrange
                SetupModelState(60, 60, 999, 1000, 100, 100);

                // Act & Assert
                Assert.That(_model.IsClear(), Is.False);
            }

            #endregion

            #region IsFailed Tests

            [Test]
            public void IsFailed_WhenHpZero_ReturnsTrue()
            {
                // Arrange
                SetupModelState(60, 60, 0, 1000, 0, 100);

                // Act & Assert
                Assert.That(_model.IsFailed(), Is.True);
            }

            [Test]
            public void IsFailed_WhenTimeUp_ReturnsTrue()
            {
                // Arrange
                SetupModelState(0, 60, 0, 1000, 100, 100);

                // Act & Assert
                Assert.That(_model.IsFailed(), Is.True);
            }

            [Test]
            public void IsFailed_WhenBothHpAndTimeUp_ReturnsTrue()
            {
                // Arrange
                SetupModelState(0, 60, 0, 1000, 0, 100);

                // Act & Assert
                Assert.That(_model.IsFailed(), Is.True);
            }

            [Test]
            public void IsFailed_WhenHpAndTimeRemaining_ReturnsFalse()
            {
                // Arrange
                SetupModelState(30, 60, 0, 1000, 50, 100);

                // Act & Assert
                Assert.That(_model.IsFailed(), Is.False);
            }

            #endregion

            #region CanPause Tests

            [Test]
            public void CanPause_WhenStageStateIsStart_ReturnsTrue()
            {
                // Arrange
                _model.StageState = GameStageState.Start;

                // Act & Assert
                Assert.That(_model.CanPause(), Is.True);
            }

            [Test]
            public void CanPause_WhenStageStateIsNotStart_ReturnsFalse()
            {
                // Arrange & Act & Assert
                _model.StageState = GameStageState.None;
                Assert.That(_model.CanPause(), Is.False);

                _model.StageState = GameStageState.Ready;
                Assert.That(_model.CanPause(), Is.False);

                _model.StageState = GameStageState.Result;
                Assert.That(_model.CanPause(), Is.False);
            }

            #endregion

            #region UpdateStageResult Tests

            [Test]
            public void UpdateStageResult_WhenClear_SetsResultToClear()
            {
                // Arrange
                SetupModelState(30, 60, 1000, 1000, 100, 100);
                _model.StageResult = GameStageResult.None;

                // Act
                _model.UpdateStageResult();

                // Assert
                Assert.That(_model.StageResult, Is.EqualTo(GameStageResult.Clear));
            }

            [Test]
            public void UpdateStageResult_WhenFailed_SetsResultToFailed()
            {
                // Arrange
                SetupModelState(0, 60, 500, 1000, 100, 100); // TimeUp
                _model.StageResult = GameStageResult.None;

                // Act
                _model.UpdateStageResult();

                // Assert
                Assert.That(_model.StageResult, Is.EqualTo(GameStageResult.Failed));
            }

            [Test]
            public void UpdateStageResult_WhenClearAndFailed_SetsResultToFailed()
            {
                // Arrange - Clear (MaxPoint reached) but also Failed (TimeUp)
                SetupModelState(0, 60, 1000, 1000, 100, 100);
                _model.StageResult = GameStageResult.None;

                // Act
                _model.UpdateStageResult();

                // Assert - Failed takes priority because it's checked after Clear
                Assert.That(_model.StageResult, Is.EqualTo(GameStageResult.Failed));
            }

            [Test]
            public void UpdateStageResult_WhenNeitherClearNorFailed_KeepsNone()
            {
                // Arrange
                SetupModelState(30, 60, 500, 1000, 100, 100);
                _model.StageResult = GameStageResult.None;

                // Act
                _model.UpdateStageResult();

                // Assert
                Assert.That(_model.StageResult, Is.EqualTo(GameStageResult.None));
            }

            #endregion
        }

        #endregion
    }
}
