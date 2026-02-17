using System.Collections.Generic;
using Game.MVP.Survivor.SaveData;
using Game.MVP.Survivor.Scenes.ViewModels;
using NUnit.Framework;

namespace Game.Tests.MVP
{
    [TestFixture]
    public class TotalResultSceneViewModelTests
    {
        private TotalResultSceneViewModel _viewModel;

        [SetUp]
        public void Setup()
        {
            _viewModel = new TotalResultSceneViewModel();
        }

        #region IsOverallVictory Tests

        [Test]
        public void IsOverallVictory_WithEmptyResults_ReturnsFalse()
        {
            var session = new SurvivorStageSession
            {
                StageResults = new List<SurvivorStageResultData>()
            };

            Assert.That(_viewModel.IsOverallVictory(session), Is.False);
        }

        [Test]
        public void IsOverallVictory_AllVictory_ReturnsTrue()
        {
            var session = new SurvivorStageSession
            {
                StageResults = new List<SurvivorStageResultData>
                {
                    new() { StageId = 1, IsVictory = true },
                    new() { StageId = 2, IsVictory = true },
                    new() { StageId = 3, IsVictory = true }
                }
            };

            Assert.That(_viewModel.IsOverallVictory(session), Is.True);
        }

        [Test]
        public void IsOverallVictory_OneDefeat_ReturnsFalse()
        {
            var session = new SurvivorStageSession
            {
                StageResults = new List<SurvivorStageResultData>
                {
                    new() { StageId = 1, IsVictory = true },
                    new() { StageId = 2, IsVictory = false },
                    new() { StageId = 3, IsVictory = true }
                }
            };

            Assert.That(_viewModel.IsOverallVictory(session), Is.False);
        }

        [Test]
        public void IsOverallVictory_SingleVictory_ReturnsTrue()
        {
            var session = new SurvivorStageSession
            {
                StageResults = new List<SurvivorStageResultData>
                {
                    new() { StageId = 1, IsVictory = true }
                }
            };

            Assert.That(_viewModel.IsOverallVictory(session), Is.True);
        }

        [Test]
        public void IsOverallVictory_SingleDefeat_ReturnsFalse()
        {
            var session = new SurvivorStageSession
            {
                StageResults = new List<SurvivorStageResultData>
                {
                    new() { StageId = 1, IsVictory = false }
                }
            };

            Assert.That(_viewModel.IsOverallVictory(session), Is.False);
        }

        [Test]
        public void IsOverallVictory_AllDefeat_ReturnsFalse()
        {
            var session = new SurvivorStageSession
            {
                StageResults = new List<SurvivorStageResultData>
                {
                    new() { StageId = 1, IsVictory = false },
                    new() { StageId = 2, IsVictory = false }
                }
            };

            Assert.That(_viewModel.IsOverallVictory(session), Is.False);
        }

        #endregion

        #region BuildScoreRequest Tests

        [Test]
        public void BuildScoreRequest_MapsStageId()
        {
            var result = new SurvivorStageResultData { StageId = 5, Score = 0, Kills = 0 };
            var request = _viewModel.BuildScoreRequest(result, 1);
            Assert.That(request.StageId, Is.EqualTo(5));
        }

        [Test]
        public void BuildScoreRequest_MapsScore()
        {
            var result = new SurvivorStageResultData { StageId = 1, Score = 12345, Kills = 0 };
            var request = _viewModel.BuildScoreRequest(result, 1);
            Assert.That(request.Score, Is.EqualTo(12345));
        }

        [Test]
        public void BuildScoreRequest_MapsClearTime()
        {
            var result = new SurvivorStageResultData { StageId = 1, Score = 0, ClearTime = 98.5f, Kills = 0 };
            var request = _viewModel.BuildScoreRequest(result, 1);
            Assert.That(request.ClearTime, Is.EqualTo(98.5f));
        }

        [Test]
        public void BuildScoreRequest_MapsCurrentWave()
        {
            var result = new SurvivorStageResultData { StageId = 1, Score = 0, Kills = 0 };
            var request = _viewModel.BuildScoreRequest(result, 7);
            Assert.That(request.WaveReached, Is.EqualTo(7));
        }

        [Test]
        public void BuildScoreRequest_MapsEnemiesDefeated()
        {
            var result = new SurvivorStageResultData { StageId = 1, Score = 0, Kills = 42 };
            var request = _viewModel.BuildScoreRequest(result, 1);
            Assert.That(request.EnemiesDefeated, Is.EqualTo(42));
        }

        #endregion

        #region TotalGroupScore Tests (SurvivorStageSession computed property)

        [Test]
        public void TotalGroupScore_WithEmptyResults_ReturnsZero()
        {
            var session = new SurvivorStageSession
            {
                StageResults = new List<SurvivorStageResultData>()
            };

            Assert.That(session.TotalGroupScore, Is.EqualTo(0));
        }

        [Test]
        public void TotalGroupScore_SingleResult_ReturnsScore()
        {
            var session = new SurvivorStageSession
            {
                StageResults = new List<SurvivorStageResultData>
                {
                    new() { Score = 3000 }
                }
            };

            Assert.That(session.TotalGroupScore, Is.EqualTo(3000));
        }

        [Test]
        public void TotalGroupScore_MultipleResults_ReturnsSumOfScores()
        {
            var session = new SurvivorStageSession
            {
                StageResults = new List<SurvivorStageResultData>
                {
                    new() { Score = 1000 },
                    new() { Score = 2000 },
                    new() { Score = 3000 }
                }
            };

            Assert.That(session.TotalGroupScore, Is.EqualTo(6000));
        }

        #endregion

        #region TotalGroupKills Tests (SurvivorStageSession computed property)

        [Test]
        public void TotalGroupKills_WithEmptyResults_ReturnsZero()
        {
            var session = new SurvivorStageSession
            {
                StageResults = new List<SurvivorStageResultData>()
            };

            Assert.That(session.TotalGroupKills, Is.EqualTo(0));
        }

        [Test]
        public void TotalGroupKills_MultipleResults_ReturnsSumOfKills()
        {
            var session = new SurvivorStageSession
            {
                StageResults = new List<SurvivorStageResultData>
                {
                    new() { Kills = 10 },
                    new() { Kills = 25 },
                    new() { Kills = 15 }
                }
            };

            Assert.That(session.TotalGroupKills, Is.EqualTo(50));
        }

        #endregion
    }
}
