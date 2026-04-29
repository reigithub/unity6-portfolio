using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Client.MasterData;
using Game.MVP.Survivor.Scenes.Models;
using Game.Shared.Network.Survivor;
using Game.Shared.Services;
using MasterMemory;
using MessagePack;
using MessagePack.Resolvers;
using NSubstitute;
using NUnit.Framework;
using Unity.Collections;

namespace Game.Tests.MVP
{
    [TestFixture]
    public class SurvivorNetworkStageModelTests
    {
        private SurvivorNetworkStageModel _model;

        [SetUp]
        public void Setup()
        {
            _model = new SurvivorNetworkStageModel();
        }

        [TearDown]
        public void TearDown()
        {
            _model?.Dispose();
        }

        #region Initialize Tests

        [Test]
        public void Initialize_LoadsStageMaster()
        {
            var model = CreateModelWithStageMaster(stageId: 1, timeLimit: 60);

            model.Initialize(1);

            Assert.That(model.StageMaster.Id, Is.EqualTo(1));
            Assert.That(model.StageMaster.TimeLimit, Is.EqualTo(60));

            model.Dispose();
        }

        [Test]
        public void Initialize_WhenStageNotFound_Throws()
        {
            var model = CreateModelWithStageMaster(stageId: 1, timeLimit: 60);

            Assert.Throws<InvalidOperationException>(() => model.Initialize(999));

            model.Dispose();
        }

        #endregion

        #region IsTimeUp Tests

        [Test]
        public void IsTimeUp_WhenNoTimeLimit_ReturnsFalse()
        {
            // _stageMaster is null, so TimeLimit is 0
            _model.GameTime.Value = 1000f;

            Assert.That(_model.IsTimeUp, Is.False);
        }

        [Test]
        public void IsTimeUp_WhenTimeReached_ReturnsTrue()
        {
            SetStageMasterTimeLimit(_model, 60f);
            _model.GameTime.Value = 60f;

            Assert.That(_model.IsTimeUp, Is.True);
        }

        [Test]
        public void IsTimeUp_WhenTimeExceeded_ReturnsTrue()
        {
            SetStageMasterTimeLimit(_model, 60f);
            _model.GameTime.Value = 70f;

            Assert.That(_model.IsTimeUp, Is.True);
        }

        [Test]
        public void IsTimeUp_WhenTimeNotReached_ReturnsFalse()
        {
            SetStageMasterTimeLimit(_model, 60f);
            _model.GameTime.Value = 30f;

            Assert.That(_model.IsTimeUp, Is.False);
        }

        #endregion

        #region NetworkResult Tests

        [Test]
        public void HasNetworkResult_InitiallyFalse()
        {
            Assert.That(_model.HasNetworkResult, Is.False);
        }

        [Test]
        public void SetNetworkResult_UpdatesResult()
        {
            var result = new SurvivorNetworkGameResult
            {
                IsVictory = true,
                ClearTime = 123.45f,
                TotalKills = 42
            };

            _model.SetNetworkResult(result);

            Assert.That(_model.HasNetworkResult, Is.True);
            Assert.That(_model.NetworkResult.IsVictory, Is.True);
            Assert.That(_model.NetworkResult.ClearTime, Is.EqualTo(123.45f));
            Assert.That(_model.NetworkResult.TotalKills, Is.EqualTo(42));
        }

        #endregion

        #region PlayerContributions Tests

        [Test]
        public void PlayerContributions_InitiallyEmpty()
        {
            Assert.That(_model.PlayerContributions, Is.Not.Null);
            Assert.That(_model.PlayerContributions.Count, Is.EqualTo(0));
        }

        [Test]
        public void SetPlayerContributions_UpdatesCollection()
        {
            var contributions = new List<SurvivorNetworkPlayerResult>
            {
                new() { UserId = new FixedString64Bytes("u1"), Score = 100, TotalKills = 10, Level = 3 },
                new() { UserId = new FixedString64Bytes("u2"), Score = 80, TotalKills = 8, Level = 2 },
            };

            _model.SetPlayerContributions(contributions);

            Assert.That(_model.PlayerContributions.Count, Is.EqualTo(2));
            Assert.That(_model.PlayerContributions[0].Score, Is.EqualTo(100));
            Assert.That(_model.PlayerContributions[1].Score, Is.EqualTo(80));
        }

        [Test]
        public void SetPlayerContributions_WithNull_ClearsCollection()
        {
            _model.SetPlayerContributions(new List<SurvivorNetworkPlayerResult>
            {
                new() { Score = 100 }
            });

            _model.SetPlayerContributions(null);

            Assert.That(_model.PlayerContributions.Count, Is.EqualTo(0));
        }

        [Test]
        public void SetPlayerContributions_ReplacesExisting()
        {
            _model.SetPlayerContributions(new List<SurvivorNetworkPlayerResult>
            {
                new() { Score = 100 }
            });
            _model.SetPlayerContributions(new List<SurvivorNetworkPlayerResult>
            {
                new() { Score = 200 },
                new() { Score = 50 }
            });

            Assert.That(_model.PlayerContributions.Count, Is.EqualTo(2));
            Assert.That(_model.PlayerContributions[0].Score, Is.EqualTo(200));
        }

        #endregion

        #region Dispose Tests

        [Test]
        public void Dispose_CanBeCalledWithoutException()
        {
            var model = new SurvivorNetworkStageModel();
            Assert.DoesNotThrow(() => model.Dispose());
        }

        [Test]
        public void Dispose_ClearsPlayerContributions()
        {
            _model.SetPlayerContributions(new List<SurvivorNetworkPlayerResult>
            {
                new() { Score = 100 }
            });

            _model.Dispose();

            Assert.That(_model.PlayerContributions.Count, Is.EqualTo(0));
        }

        #endregion

        #region Helpers

        /// <summary>
        /// StageMaster を含む MemoryDatabase を構築して SurvivorNetworkStageModel を返す。
        /// </summary>
        private static SurvivorNetworkStageModel CreateModelWithStageMaster(int stageId, int timeLimit)
        {
            var formatterResolver = CompositeResolver.Create(
                MasterMemoryResolver.Instance,
                StandardResolver.Instance
            );
            var builder = new DatabaseBuilder(formatterResolver);

            builder.Append(new[]
            {
                new SurvivorStageMaster { Id = stageId, Name = "TestStage", TimeLimit = timeLimit, Difficulty = 1 }
            });

            var binary = builder.Build();
            var memoryDb = new MemoryDatabase(binary, formatterResolver: formatterResolver);

            var masterDataService = Substitute.For<IMasterDataService>();
            masterDataService.MemoryDatabase.Returns(memoryDb);

            return new SurvivorNetworkStageModel(masterDataService);
        }

        /// <summary>
        /// 既存インスタンスの `_stageMaster` フィールドをリフレクションで設定する。
        /// IsTimeUp 系のロジックテスト用（Initialize を介さずにマスターを注入）。
        /// </summary>
        private static void SetStageMasterTimeLimit(SurvivorNetworkStageModel model, float timeLimit)
        {
            var stageMaster = new SurvivorStageMaster
            {
                Id = 1,
                TimeLimit = (int)timeLimit
            };

            var field = typeof(SurvivorNetworkStageModel).GetField("_stageMaster", BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(model, stageMaster);
        }

        #endregion
    }
}
