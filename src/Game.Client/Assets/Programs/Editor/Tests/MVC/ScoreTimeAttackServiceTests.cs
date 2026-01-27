using Game.ScoreTimeAttack.Data;
using Game.ScoreTimeAttack.Enums;
using Game.ScoreTimeAttack.Services;
using NUnit.Framework;

namespace Game.Tests.MVC
{
    [TestFixture]
    public class ScoreTimeAttackServiceTests
    {
        private ScoreTimeAttackStageService _service;

        [SetUp]
        public void Setup()
        {
            _service = new ScoreTimeAttackStageService();
        }

        #region TryAddResult Tests

        [Test]
        public void TryAddResult_FirstResult_ReturnsTrue()
        {
            // Arrange
            var result = CreateResult(stageId: 1);

            // Act
            var success = _service.TryAddResult(result);

            // Assert
            Assert.That(success, Is.True);
        }

        [Test]
        public void TryAddResult_DuplicateStageId_ReturnsFalse()
        {
            // Arrange
            var result1 = CreateResult(stageId: 1, currentPoint: 100);
            var result2 = CreateResult(stageId: 1, currentPoint: 200);

            // Act
            _service.TryAddResult(result1);
            var success = _service.TryAddResult(result2);

            // Assert
            Assert.That(success, Is.False);
        }

        [Test]
        public void TryAddResult_DifferentStageIds_AllSucceed()
        {
            // Arrange & Act
            var success1 = _service.TryAddResult(CreateResult(stageId: 1));
            var success2 = _service.TryAddResult(CreateResult(stageId: 2));
            var success3 = _service.TryAddResult(CreateResult(stageId: 3));

            // Assert
            Assert.That(success1, Is.True);
            Assert.That(success2, Is.True);
            Assert.That(success3, Is.True);
        }

        #endregion

        #region CreateTotalResult Tests

        [Test]
        public void CreateTotalResult_ReturnsAllResults()
        {
            // Arrange
            _service.TryAddResult(CreateResult(stageId: 1, currentPoint: 100));
            _service.TryAddResult(CreateResult(stageId: 2, currentPoint: 200));
            _service.TryAddResult(CreateResult(stageId: 3, currentPoint: 300));

            // Act
            var totalResult = _service.CreateTotalResult();

            // Assert
            Assert.That(totalResult.StageResults.Length, Is.EqualTo(3));
        }

        [Test]
        public void CreateTotalResult_ClearsResults()
        {
            // Arrange
            _service.TryAddResult(CreateResult(stageId: 1));
            _service.TryAddResult(CreateResult(stageId: 2));

            // Act
            _service.CreateTotalResult();

            // Try adding the same stage IDs again
            var success1 = _service.TryAddResult(CreateResult(stageId: 1));
            var success2 = _service.TryAddResult(CreateResult(stageId: 2));

            // Assert
            Assert.That(success1, Is.True);
            Assert.That(success2, Is.True);
        }

        [Test]
        public void CreateTotalResult_WhenEmpty_ReturnsEmptyArray()
        {
            // Act
            var totalResult = _service.CreateTotalResult();

            // Assert
            Assert.That(totalResult.StageResults, Is.Not.Null);
            Assert.That(totalResult.StageResults.Length, Is.EqualTo(0));
        }

        [Test]
        public void CreateTotalResult_PreservesResultData()
        {
            // Arrange
            var result = CreateResult(
                stageId: 1,
                currentTime: 30,
                totalTime: 60,
                currentPoint: 500,
                maxPoint: 1000,
                currentHp: 80,
                maxHp: 100
            );
            _service.TryAddResult(result);

            // Act
            var totalResult = _service.CreateTotalResult();

            // Assert
            var savedResult = totalResult.StageResults[0];
            Assert.That(savedResult.StageId, Is.EqualTo(1));
            Assert.That(savedResult.CurrentTime, Is.EqualTo(30));
            Assert.That(savedResult.TotalTime, Is.EqualTo(60));
            Assert.That(savedResult.CurrentPoint, Is.EqualTo(500));
            Assert.That(savedResult.MaxPoint, Is.EqualTo(1000));
            Assert.That(savedResult.PlayerCurrentHp, Is.EqualTo(80));
            Assert.That(savedResult.PlayerMaxHp, Is.EqualTo(100));
        }

        #endregion

        #region Startup Tests

        [Test]
        public void Startup_ClearsResults()
        {
            // Arrange
            _service.TryAddResult(CreateResult(stageId: 1));
            _service.TryAddResult(CreateResult(stageId: 2));

            // Act
            _service.Startup();

            // Adding same IDs should succeed
            var success1 = _service.TryAddResult(CreateResult(stageId: 1));
            var success2 = _service.TryAddResult(CreateResult(stageId: 2));

            // Assert
            Assert.That(success1, Is.True);
            Assert.That(success2, Is.True);
        }

        [Test]
        public void Startup_WhenAlreadyEmpty_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _service.Startup());
        }

        #endregion

        #region Shutdown Tests

        [Test]
        public void Shutdown_ClearsResults()
        {
            // Arrange
            _service.TryAddResult(CreateResult(stageId: 1));
            _service.TryAddResult(CreateResult(stageId: 2));

            // Act
            _service.Shutdown();

            // Adding same IDs should succeed
            var success = _service.TryAddResult(CreateResult(stageId: 1));

            // Assert
            Assert.That(success, Is.True);
        }

        [Test]
        public void Shutdown_WhenAlreadyEmpty_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _service.Shutdown());
        }

        #endregion

        #region Integration Tests

        [Test]
        public void FullWorkflow_MultipleStages()
        {
            // Arrange - Start service
            _service.Startup();

            // Add stage results
            Assert.That(_service.TryAddResult(CreateResult(stageId: 1, currentPoint: 100)), Is.True);
            Assert.That(_service.TryAddResult(CreateResult(stageId: 2, currentPoint: 200)), Is.True);
            Assert.That(_service.TryAddResult(CreateResult(stageId: 3, currentPoint: 300)), Is.True);

            // Create total result
            var totalResult = _service.CreateTotalResult();

            // Assert
            Assert.That(totalResult.StageResults.Length, Is.EqualTo(3));

            // Shutdown
            _service.Shutdown();

            // Verify clean state
            totalResult = _service.CreateTotalResult();
            Assert.That(totalResult.StageResults.Length, Is.EqualTo(0));
        }

        [Test]
        public void FullWorkflow_DuplicateHandling()
        {
            // Arrange
            _service.Startup();

            // Add first result for stage 1
            Assert.That(_service.TryAddResult(CreateResult(stageId: 1, currentPoint: 100)), Is.True);

            // Try to add duplicate - should fail
            Assert.That(_service.TryAddResult(CreateResult(stageId: 1, currentPoint: 999)), Is.False);

            // Create result - should have original data
            var totalResult = _service.CreateTotalResult();
            Assert.That(totalResult.StageResults[0].CurrentPoint, Is.EqualTo(100));
        }

        #endregion

        #region Helper Methods

        private ScoreTimeAttackStageResultData CreateResult(
            int stageId = 1,
            int currentTime = 30,
            int totalTime = 60,
            int currentPoint = 500,
            int maxPoint = 1000,
            int currentHp = 100,
            int maxHp = 100,
            GameStageResult stageResult = GameStageResult.Clear,
            int? nextStageId = null)
        {
            return new ScoreTimeAttackStageResultData
            {
                StageId = stageId,
                CurrentTime = currentTime,
                TotalTime = totalTime,
                CurrentPoint = currentPoint,
                MaxPoint = maxPoint,
                PlayerCurrentHp = currentHp,
                PlayerMaxHp = maxHp,
                StageResult = stageResult,
                NextStageId = nextStageId
            };
        }

        #endregion
    }
}
