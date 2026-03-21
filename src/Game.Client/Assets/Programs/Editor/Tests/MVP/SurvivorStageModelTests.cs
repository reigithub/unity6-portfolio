using System;
using System.Reflection;
using Game.Client.MasterData;
using Game.MVP.Survivor.Scenes.Models;
using Game.Shared.Services;
using MasterMemory;
using MessagePack;
using MessagePack.Resolvers;
using NSubstitute;
using NUnit.Framework;

namespace Game.Tests.MVP
{
    [TestFixture]
    public class SurvivorStageModelTests
    {
        private SurvivorStageModel _model;

        [SetUp]
        public void Setup()
        {
            _model = new SurvivorStageModel();
            // デフォルト値を設定
            _model.CurrentHp.Value = 100;
            _model.MaxHp.Value = 100;
            _model.Level.Value = 1;
            _model.Experience.Value = 0;
            _model.ExperienceToNextLevel.Value = 10;
            _model.DamageBonus.Value = 0;
            _model.TotalKills.Value = 0;
            _model.Score.Value = 0;
            _model.GameTime.Value = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            _model?.Dispose();
        }

        #region TakeDamage Tests

        [Test]
        public void TakeDamage_ReducesHp()
        {
            // Arrange
            _model.CurrentHp.Value = 100;

            // Act
            _model.TakeDamage(30);

            // Assert
            Assert.That(_model.CurrentHp.Value, Is.EqualTo(70));
        }

        [Test]
        public void TakeDamage_ClampsToZero()
        {
            // Arrange
            _model.CurrentHp.Value = 50;

            // Act
            _model.TakeDamage(100);

            // Assert
            Assert.That(_model.CurrentHp.Value, Is.EqualTo(0));
        }

        [Test]
        public void TakeDamage_WithZeroDamage_DoesNotChangeHp()
        {
            // Arrange
            _model.CurrentHp.Value = 100;

            // Act
            _model.TakeDamage(0);

            // Assert
            Assert.That(_model.CurrentHp.Value, Is.EqualTo(100));
        }

        [Test]
        public void TakeDamage_MultipleDamages_AccumulatesCorrectly()
        {
            // Arrange
            _model.CurrentHp.Value = 100;

            // Act
            _model.TakeDamage(20);
            _model.TakeDamage(30);
            _model.TakeDamage(15);

            // Assert
            Assert.That(_model.CurrentHp.Value, Is.EqualTo(35));
        }

        #endregion

        #region Heal Tests

        [Test]
        public void Heal_IncreasesHp()
        {
            // Arrange
            _model.CurrentHp.Value = 50;
            _model.MaxHp.Value = 100;

            // Act
            _model.Heal(30);

            // Assert
            Assert.That(_model.CurrentHp.Value, Is.EqualTo(80));
        }

        [Test]
        public void Heal_ClampsToMaxHp()
        {
            // Arrange
            _model.CurrentHp.Value = 80;
            _model.MaxHp.Value = 100;

            // Act
            _model.Heal(50);

            // Assert
            Assert.That(_model.CurrentHp.Value, Is.EqualTo(100));
        }

        [Test]
        public void Heal_WhenAlreadyFull_DoesNotExceedMax()
        {
            // Arrange
            _model.CurrentHp.Value = 100;
            _model.MaxHp.Value = 100;

            // Act
            _model.Heal(50);

            // Assert
            Assert.That(_model.CurrentHp.Value, Is.EqualTo(100));
        }

        [Test]
        public void Heal_WithZeroAmount_DoesNotChangeHp()
        {
            // Arrange
            _model.CurrentHp.Value = 50;

            // Act
            _model.Heal(0);

            // Assert
            Assert.That(_model.CurrentHp.Value, Is.EqualTo(50));
        }

        #endregion

        #region IsDead Tests

        [Test]
        public void IsDead_WhenHpZero_ReturnsTrue()
        {
            // Arrange
            _model.CurrentHp.Value = 0;

            // Assert
            Assert.That(_model.IsDead, Is.True);
        }

        [Test]
        public void IsDead_WhenHpPositive_ReturnsFalse()
        {
            // Arrange
            _model.CurrentHp.Value = 1;

            // Assert
            Assert.That(_model.IsDead, Is.False);
        }

        [Test]
        public void IsDead_AfterTakingLethalDamage_ReturnsTrue()
        {
            // Arrange
            _model.CurrentHp.Value = 50;

            // Act
            _model.TakeDamage(100);

            // Assert
            Assert.That(_model.IsDead, Is.True);
        }

        #endregion

        #region AddKill Tests

        [Test]
        public void AddKill_IncrementsTotalKills()
        {
            // Arrange
            _model.TotalKills.Value = 0;

            // Act
            _model.AddKill();

            // Assert
            Assert.That(_model.TotalKills.Value, Is.EqualTo(1));
        }

        [Test]
        public void AddKill_MultipleKills_AccumulatesCorrectly()
        {
            // Arrange
            _model.TotalKills.Value = 0;

            // Act
            for (int i = 0; i < 10; i++)
            {
                _model.AddKill();
            }

            // Assert
            Assert.That(_model.TotalKills.Value, Is.EqualTo(10));
        }

        [Test]
        public void AddKill_DoesNotAffectScore()
        {
            // Arrange
            _model.Score.Value = 100;
            _model.TotalKills.Value = 0;

            // Act
            _model.AddKill();

            // Assert
            Assert.That(_model.Score.Value, Is.EqualTo(100));
        }

        #endregion

        #region AddWaveClearScore Tests

        [Test]
        public void AddWaveClearScore_CalculatesCorrectScore()
        {
            // Arrange
            _model.Score.Value = 0;
            int waveNumber = 1;
            float remainingTime = 30f;
            int scoreMultiplier = 100;
            int currentHp = 80;
            int maxHp = 100;

            // Act
            _model.AddWaveClearScore(waveNumber, remainingTime, scoreMultiplier, currentHp, maxHp);

            // Assert
            // Score = remainingTime * scoreMultiplier * (currentHp / maxHp)
            // = 30 * 100 * 0.8 = 2400
            Assert.That(_model.Score.Value, Is.EqualTo(2400));
        }

        [Test]
        public void AddWaveClearScore_WithFullHp_UsesFullMultiplier()
        {
            // Arrange
            _model.Score.Value = 0;

            // Act
            _model.AddWaveClearScore(1, 30f, 100, 100, 100);

            // Assert
            // Score = 30 * 100 * 1.0 = 3000
            Assert.That(_model.Score.Value, Is.EqualTo(3000));
        }

        [Test]
        public void AddWaveClearScore_WithHalfHp_UsesHalfMultiplier()
        {
            // Arrange
            _model.Score.Value = 0;

            // Act
            _model.AddWaveClearScore(1, 30f, 100, 50, 100);

            // Assert
            // Score = 30 * 100 * 0.5 = 1500
            Assert.That(_model.Score.Value, Is.EqualTo(1500));
        }

        [Test]
        public void AddWaveClearScore_WithZeroRemainingTime_DoesNotAddScore()
        {
            // Arrange
            _model.Score.Value = 100;

            // Act
            _model.AddWaveClearScore(1, 0f, 100, 100, 100);

            // Assert
            Assert.That(_model.Score.Value, Is.EqualTo(100));
        }

        [Test]
        public void AddWaveClearScore_WithNegativeRemainingTime_DoesNotAddScore()
        {
            // Arrange
            _model.Score.Value = 100;

            // Act
            _model.AddWaveClearScore(1, -10f, 100, 100, 100);

            // Assert
            Assert.That(_model.Score.Value, Is.EqualTo(100));
        }

        [Test]
        public void AddWaveClearScore_WithZeroMaxHp_UsesFallback()
        {
            // Arrange
            _model.Score.Value = 0;

            // Act
            _model.AddWaveClearScore(1, 30f, 100, 0, 0);

            // Assert
            // hpRatio fallback to 1.0 when maxHp is 0
            // Score = 30 * 100 * 1.0 = 3000
            Assert.That(_model.Score.Value, Is.EqualTo(3000));
        }

        [Test]
        public void AddWaveClearScore_AccumulatesAcrossWaves()
        {
            // Arrange
            _model.Score.Value = 0;

            // Act
            _model.AddWaveClearScore(1, 30f, 100, 100, 100); // +3000
            _model.AddWaveClearScore(2, 20f, 150, 80, 100);  // +2400
            _model.AddWaveClearScore(3, 10f, 200, 60, 100);  // +1200

            // Assert
            Assert.That(_model.Score.Value, Is.EqualTo(6600));
        }

        #endregion

        #region GetDamageMultiplier Tests

        [Test]
        public void GetDamageMultiplier_WithZeroBonus_ReturnsOne()
        {
            // Arrange
            _model.DamageBonus.Value = 0;

            // Act
            var multiplier = _model.GetDamageMultiplier();

            // Assert
            Assert.That(multiplier, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void GetDamageMultiplier_With10000Bonus_ReturnsTwo()
        {
            // Arrange
            // ToRate() converts 10000 to 1.0
            _model.DamageBonus.Value = 10000;

            // Act
            var multiplier = _model.GetDamageMultiplier();

            // Assert
            // 1 + 1.0 = 2.0
            Assert.That(multiplier, Is.EqualTo(2f).Within(0.001f));
        }

        [Test]
        public void GetDamageMultiplier_With5000Bonus_ReturnsOnePointFive()
        {
            // Arrange
            _model.DamageBonus.Value = 5000;

            // Act
            var multiplier = _model.GetDamageMultiplier();

            // Assert
            // 1 + 0.5 = 1.5
            Assert.That(multiplier, Is.EqualTo(1.5f).Within(0.001f));
        }

        #endregion

        #region IsTimeUp Tests

        [Test]
        public void IsTimeUp_WhenNoTimeLimit_ReturnsFalse()
        {
            // Arrange - _stageMaster is null, so TimeLimit is 0
            _model.GameTime.Value = 1000f;

            // Assert
            Assert.That(_model.IsTimeUp, Is.False);
        }

        [Test]
        public void IsTimeUp_WithTimeLimit_WhenTimeReached_ReturnsTrue()
        {
            // Arrange
            SetStageMasterTimeLimit(60f);
            _model.GameTime.Value = 60f;

            // Assert
            Assert.That(_model.IsTimeUp, Is.True);
        }

        [Test]
        public void IsTimeUp_WithTimeLimit_WhenTimeExceeded_ReturnsTrue()
        {
            // Arrange
            SetStageMasterTimeLimit(60f);
            _model.GameTime.Value = 70f;

            // Assert
            Assert.That(_model.IsTimeUp, Is.True);
        }

        [Test]
        public void IsTimeUp_WithTimeLimit_WhenTimeNotReached_ReturnsFalse()
        {
            // Arrange
            SetStageMasterTimeLimit(60f);
            _model.GameTime.Value = 30f;

            // Assert
            Assert.That(_model.IsTimeUp, Is.False);
        }

        private void SetStageMasterTimeLimit(float timeLimit)
        {
            // SurvivorStageMasterはreadonly structなのでリフレクションで設定
            var stageMaster = new SurvivorStageMaster
            {
                Id = 1,
                TimeLimit = (int)timeLimit
            };

            var field = typeof(SurvivorStageModel).GetField("_stageMaster", BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(_model, stageMaster);
        }

        #endregion

        #region AddExperience Tests (without MasterData)

        [Test]
        public void AddExperience_AddsExperience()
        {
            // Arrange
            _model.Experience.Value = 0;
            _model.ExperienceToNextLevel.Value = 100;

            // Act
            _model.AddExperience(30);

            // Assert
            Assert.That(_model.Experience.Value, Is.EqualTo(30));
        }

        [Test]
        public void AddExperience_WhenNotReachingThreshold_DoesNotLevelUp()
        {
            // Arrange
            _model.Level.Value = 1;
            _model.Experience.Value = 0;
            _model.ExperienceToNextLevel.Value = 100;

            // Act
            _model.AddExperience(50);

            // Assert
            Assert.That(_model.Level.Value, Is.EqualTo(1));
            Assert.That(_model.Experience.Value, Is.EqualTo(50));
        }

        [Test]
        public void AddExperience_AccumulatesCorrectly()
        {
            // Arrange
            _model.Experience.Value = 0;
            _model.ExperienceToNextLevel.Value = 100;

            // Act
            _model.AddExperience(30);
            _model.AddExperience(20);
            _model.AddExperience(10);

            // Assert
            Assert.That(_model.Experience.Value, Is.EqualTo(60));
        }

        #endregion

        #region AddExperience Tests (with MasterData)

        [Test]
        public void AddExperience_WhenReachingThreshold_LevelsUp()
        {
            // Arrange
            var model = CreateModelWithMasterData();
            model.Initialize(1, 1);
            // Initialize sets Level=1 from master (RequiredExp=100, MaxHp=100)

            // Act
            model.AddExperience(100);

            // Assert
            Assert.That(model.Level.Value, Is.EqualTo(2));
            Assert.That(model.MaxHp.Value, Is.EqualTo(120));
            Assert.That(model.ExperienceToNextLevel.Value, Is.EqualTo(150));
            Assert.That(model.Experience.Value, Is.EqualTo(0));

            model.Dispose();
        }

        [Test]
        public void AddExperience_MultipleLevelUps()
        {
            // Arrange
            var model = CreateModelWithMasterData();
            model.Initialize(1, 1);
            // Level 1: RequiredExp=100, Level 2: RequiredExp=150

            // Act - 250 exp = Level1(100) + Level2(150) → Level 3
            model.AddExperience(250);

            // Assert
            Assert.That(model.Level.Value, Is.EqualTo(3));
            Assert.That(model.MaxHp.Value, Is.EqualTo(150));
            Assert.That(model.Experience.Value, Is.EqualTo(0));

            model.Dispose();
        }

        [Test]
        public void AddExperience_LevelUp_IncreasesCurrentHp()
        {
            // Arrange
            var model = CreateModelWithMasterData();
            model.Initialize(1, 1);
            // After Initialize: MaxHp=100, CurrentHp=100
            model.TakeDamage(30); // CurrentHp=70

            // Act - Level up: MaxHp 100→120, HP increase = 20
            model.AddExperience(100);

            // Assert - CurrentHp should increase by MaxHp difference (20)
            Assert.That(model.CurrentHp.Value, Is.EqualTo(90)); // 70 + 20
            Assert.That(model.MaxHp.Value, Is.EqualTo(120));

            model.Dispose();
        }

        private static SurvivorStageModel CreateModelWithMasterData()
        {
            var formatterResolver = CompositeResolver.Create(
                MasterMemoryResolver.Instance,
                StandardResolver.Instance
            );
            var builder = new DatabaseBuilder(formatterResolver);

            builder.Append(new[]
            {
                new SurvivorPlayerMaster { Id = 1, Name = "TestPlayer", StartingWeaponId = 1 }
            });
            builder.Append(new[]
            {
                new SurvivorStageMaster { Id = 1, Name = "TestStage", TimeLimit = 60, Difficulty = 1 }
            });
            builder.Append(new[]
            {
                new SurvivorPlayerLevelMaster
                {
                    PlayerId = 1, Level = 1,
                    MaxHp = 100, RequiredExp = 100,
                    DamageBonus = 0, WeaponChoiceCount = 3
                },
                new SurvivorPlayerLevelMaster
                {
                    PlayerId = 1, Level = 2,
                    MaxHp = 120, RequiredExp = 150,
                    DamageBonus = 500, WeaponChoiceCount = 3
                },
                new SurvivorPlayerLevelMaster
                {
                    PlayerId = 1, Level = 3,
                    MaxHp = 150, RequiredExp = 200,
                    DamageBonus = 1000, WeaponChoiceCount = 4
                }
            });

            var binary = builder.Build();
            var memoryDb = new MemoryDatabase(binary, formatterResolver: formatterResolver);

            var masterDataService = Substitute.For<IMasterDataService>();
            masterDataService.MemoryDatabase.Returns(memoryDb);

            return new SurvivorStageModel(masterDataService);
        }

        #endregion

        #region Dispose Tests

        [Test]
        public void Dispose_CanBeCalledWithoutException()
        {
            // Arrange
            var model = new SurvivorStageModel();

            // Act & Assert - Dispose should not throw
            Assert.DoesNotThrow(() => model.Dispose());
        }

        [Test]
        public void Dispose_MultipleCallsDoNotThrow()
        {
            // Arrange
            var model = new SurvivorStageModel();

            // Act & Assert - Multiple dispose calls should be safe
            Assert.DoesNotThrow(() =>
            {
                model.Dispose();
                model.Dispose();
            });
        }

        #endregion

        #region ForceSetHp Tests

        [Test]
        public void ForceSetHp_SetsExactValue()
        {
            _model.CurrentHp.Value = 100;
            _model.MaxHp.Value = 100;

            _model.ForceSetHp(42);

            Assert.That(_model.CurrentHp.Value, Is.EqualTo(42));
        }

        [Test]
        public void ForceSetHp_Zero_MarksDead()
        {
            _model.CurrentHp.Value = 100;
            _model.MaxHp.Value = 100;

            _model.ForceSetHp(0);

            Assert.That(_model.CurrentHp.Value, Is.EqualTo(0));
            Assert.That(_model.IsDead, Is.True);
        }

        [Test]
        public void ForceSetHp_AfterDamage_OverwritesPreviousValue()
        {
            _model.CurrentHp.Value = 100;
            _model.MaxHp.Value = 100;

            _model.TakeDamage(30); // 70
            _model.ForceSetHp(55); // サーバー権威で上書き

            Assert.That(_model.CurrentHp.Value, Is.EqualTo(55));
        }

        #endregion
    }
}