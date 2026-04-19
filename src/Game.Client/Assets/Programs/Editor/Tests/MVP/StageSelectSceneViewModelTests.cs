using System.Collections.Generic;
using Game.Client.MasterData;
using Game.MVP.Survivor.SaveData;
using Game.MVP.Survivor.Scenes;
using Game.MVP.Survivor.Scenes.ViewModels;
using NUnit.Framework;

namespace Game.Tests.MVP
{
    [TestFixture]
    public class StageSelectSceneViewModelTests
    {
        private StageSelectSceneViewModel _viewModel;
        private SurvivorSaveData _saveData;

        [SetUp]
        public void Setup()
        {
            _viewModel = new StageSelectSceneViewModel();
            _saveData = new SurvivorSaveData
            {
                UnlockedStageIds = new HashSet<int> { 1 },
                StageRecords = new Dictionary<int, SurvivorStageClearRecord>()
            };
        }

        #region BuildStageItems Tests

        [Test]
        public void BuildStageItems_WithEmptyStages_ReturnsEmptyList()
        {
            var stages = new List<SurvivorStageMaster>();
            var result = _viewModel.BuildStageItems(stages, _saveData);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void BuildStageItems_SortsById()
        {
            var stages = new List<SurvivorStageMaster>
            {
                new() { Id = 3, Name = "Stage3" },
                new() { Id = 1, Name = "Stage1" },
                new() { Id = 2, Name = "Stage2" }
            };

            var result = _viewModel.BuildStageItems(stages, _saveData);

            Assert.That(result[0].StageId, Is.EqualTo(1));
            Assert.That(result[1].StageId, Is.EqualTo(2));
            Assert.That(result[2].StageId, Is.EqualTo(3));
        }

        [Test]
        public void BuildStageItems_MapsStageId()
        {
            var stages = new List<SurvivorStageMaster>
            {
                new() { Id = 42, Name = "TestStage" }
            };
            _saveData.UnlockedStageIds.Add(42);

            var result = _viewModel.BuildStageItems(stages, _saveData);

            Assert.That(result[0].StageId, Is.EqualTo(42));
        }

        [Test]
        public void BuildStageItems_MapsStageName()
        {
            var stages = new List<SurvivorStageMaster>
            {
                new() { Id = 1, Name = "Forest" }
            };

            var result = _viewModel.BuildStageItems(stages, _saveData);

            Assert.That(result[0].StageName, Is.EqualTo("Forest"));
        }

        [Test]
        public void BuildStageItems_MapsDescription()
        {
            var stages = new List<SurvivorStageMaster>
            {
                new() { Id = 1, Name = "Stage1", Description = "A dark forest" }
            };

            var result = _viewModel.BuildStageItems(stages, _saveData);

            Assert.That(result[0].Description, Is.EqualTo("A dark forest"));
        }

        [Test]
        public void BuildStageItems_MapsDifficulty()
        {
            var stages = new List<SurvivorStageMaster>
            {
                new() { Id = 1, Name = "Stage1", Difficulty = 3 }
            };

            var result = _viewModel.BuildStageItems(stages, _saveData);

            Assert.That(result[0].Difficulty, Is.EqualTo(3));
        }

        [Test]
        public void BuildStageItems_MapsTimeLimit()
        {
            var stages = new List<SurvivorStageMaster>
            {
                new() { Id = 1, Name = "Stage1", TimeLimit = 120 }
            };

            var result = _viewModel.BuildStageItems(stages, _saveData);

            Assert.That(result[0].TimeLimit, Is.EqualTo(120));
        }

        [Test]
        public void BuildStageItems_UnlockedStage_IsUnlockedTrue()
        {
            var stages = new List<SurvivorStageMaster>
            {
                new() { Id = 1, Name = "Stage1" }
            };
            _saveData.UnlockedStageIds = new HashSet<int> { 1 };

            var result = _viewModel.BuildStageItems(stages, _saveData);

            Assert.That(result[0].IsUnlocked, Is.True);
        }

        [Test]
        public void BuildStageItems_AnyStage_IsUnlockedAlwaysTrue_TechnicalDebtTodo()
        {
            // TODO(アンロック機構の再設計): 現状ローカルセーブデータ源泉でアンロック状態が壊れる
            // 構造的不具合があるため、StageSelectSceneViewModel は常に IsUnlocked=true を返す。
            // サーバー (PostgreSQL) 側で正しい同期実装が入ったらこのテストを削除し、
            // 元の BuildStageItems_LockedStage_IsUnlockedFalse を復活させる。
            var stages = new List<SurvivorStageMaster>
            {
                new() { Id = 2, Name = "Stage2" }
            };
            _saveData.UnlockedStageIds = new HashSet<int> { 1 };

            var result = _viewModel.BuildStageItems(stages, _saveData);

            Assert.That(result[0].IsUnlocked, Is.True);
        }

        [Test]
        public void BuildStageItems_WithClearRecord_MapsRecord()
        {
            var stages = new List<SurvivorStageMaster>
            {
                new() { Id = 1, Name = "Stage1" }
            };
            var record = new SurvivorStageClearRecord
            {
                StageId = 1,
                IsCleared = true,
                HighScore = 5000,
                StarRating = 3,
                ClearCount = 2,
                BestClearTime = 45.5f
            };
            _saveData.StageRecords[1] = record;

            var result = _viewModel.BuildStageItems(stages, _saveData);

            Assert.That(result[0].IsCleared, Is.True);
            Assert.That(result[0].HighScore, Is.EqualTo(5000));
            Assert.That(result[0].StarRating, Is.EqualTo(3));
            Assert.That(result[0].ClearCount, Is.EqualTo(2));
            Assert.That(result[0].BestClearTime, Is.EqualTo(45.5f));
            Assert.That(result[0].HasBestClearTime, Is.True);
        }

        [Test]
        public void BuildStageItems_WithoutClearRecord_HasDefaultValues()
        {
            var stages = new List<SurvivorStageMaster>
            {
                new() { Id = 1, Name = "Stage1" }
            };

            var result = _viewModel.BuildStageItems(stages, _saveData);

            Assert.That(result[0].Record, Is.Null);
            Assert.That(result[0].IsCleared, Is.False);
            Assert.That(result[0].HighScore, Is.EqualTo(0));
            Assert.That(result[0].StarRating, Is.EqualTo(0));
            Assert.That(result[0].ClearCount, Is.EqualTo(0));
            Assert.That(result[0].HasBestClearTime, Is.False);
        }

        [Test]
        public void BuildStageItems_MixedUnlockAndRecords_MapsCorrectly()
        {
            var stages = new List<SurvivorStageMaster>
            {
                new() { Id = 1, Name = "Stage1" },
                new() { Id = 2, Name = "Stage2" },
                new() { Id = 3, Name = "Stage3" }
            };
            _saveData.UnlockedStageIds = new HashSet<int> { 1, 2 };
            _saveData.StageRecords[1] = new SurvivorStageClearRecord
            {
                StageId = 1, IsCleared = true, HighScore = 3000
            };

            var result = _viewModel.BuildStageItems(stages, _saveData);

            // Stage 1: unlocked, cleared
            Assert.That(result[0].IsUnlocked, Is.True);
            Assert.That(result[0].IsCleared, Is.True);
            Assert.That(result[0].HighScore, Is.EqualTo(3000));

            // Stage 2: unlocked, not cleared
            Assert.That(result[1].IsUnlocked, Is.True);
            Assert.That(result[1].IsCleared, Is.False);

            // Stage 3: アンロック機構の技術的負債一時対応により常に IsUnlocked=true
            // (本来は UnlockedStageIds に含まれないため false が期待値)
            Assert.That(result[2].IsUnlocked, Is.True);
            Assert.That(result[2].IsCleared, Is.False);
        }

        #endregion
    }
}
