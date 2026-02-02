using System;
using System.Collections.Generic;
using System.Reflection;
using Game.MVP.Survivor.Services;
using NUnit.Framework;
using R3;

namespace Game.Tests.MVP
{
    [TestFixture]
    public class SurvivorWaveManagerTests
    {
        private SurvivorStageWaveManager _manager;
        private List<int> _waveClearedEvents;
        private List<Unit> _killCountedEvents;
        private IDisposable _waveClearedSubscription;
        private IDisposable _killCountedSubscription;

        [SetUp]
        public void Setup()
        {
            _manager = new SurvivorStageWaveManager();
            _waveClearedEvents = new List<int>();
            _killCountedEvents = new List<Unit>();

            // イベント購読
            _waveClearedSubscription = _manager.OnWaveCleared.Subscribe(wave => _waveClearedEvents.Add(wave));
            _killCountedSubscription = _manager.OnKillCounted.Subscribe(unit => _killCountedEvents.Add(unit));
        }

        [TearDown]
        public void TearDown()
        {
            _waveClearedSubscription?.Dispose();
            _killCountedSubscription?.Dispose();
            _manager?.Dispose();
        }

        #region GetSpawnInfo Tests

        [Test]
        public void GetSpawnInfo_BeforeInitialize_ReturnsDefaultValues()
        {
            // Act
            var spawnInfo = _manager.GetSpawnInfo();

            // Assert
            Assert.That(spawnInfo, Is.Not.Null);
            Assert.That(spawnInfo.WaveNumber, Is.EqualTo(1));
            Assert.That(spawnInfo.EnemyCount, Is.EqualTo(5));
            Assert.That(spawnInfo.TargetKillCount, Is.EqualTo(5));
            Assert.That(spawnInfo.RequiredBossKills, Is.EqualTo(0));
            Assert.That(spawnInfo.SpawnInterval, Is.EqualTo(2f));
            Assert.That(spawnInfo.EnemySpeedMultiplier, Is.EqualTo(1f));
            Assert.That(spawnInfo.EnemyHealthMultiplier, Is.EqualTo(1f));
            Assert.That(spawnInfo.EnemyDamageMultiplier, Is.EqualTo(1f));
            Assert.That(spawnInfo.ExperienceMultiplier, Is.EqualTo(1f));
            Assert.That(spawnInfo.ScoreMultiplier, Is.EqualTo(100));
        }

        #endregion

        #region GetEnemySpawnList Tests

        [Test]
        public void GetEnemySpawnList_BeforeInitialize_ReturnsEmptyList()
        {
            // Act
            var enemyList = _manager.GetEnemySpawnList();

            // Assert
            Assert.That(enemyList, Is.Not.Null);
            Assert.That(enemyList.Count, Is.EqualTo(0));
        }

        #endregion

        #region TotalWaves Tests

        [Test]
        public void TotalWaves_BeforeInitialize_ReturnsZero()
        {
            // Assert
            Assert.That(_manager.TotalWaves, Is.EqualTo(0));
        }

        #endregion

        #region TotalTargetKills Tests

        [Test]
        public void TotalTargetKills_BeforeInitialize_ReturnsZero()
        {
            // Assert
            Assert.That(_manager.TotalTargetKills, Is.EqualTo(0));
        }

        #endregion

        #region OnEnemyKilled Tests (with reflection setup)

        [Test]
        public void OnEnemyKilled_IncrementsKillCount()
        {
            // Arrange
            SetupWaveState(targetKills: 10, currentKills: 0, requiredBossKills: 0, currentBossKills: 0);

            // Act
            _manager.OnEnemyKilled();

            // Assert
            Assert.That(_manager.EnemiesKilled.CurrentValue, Is.EqualTo(1));
        }

        [Test]
        public void OnEnemyKilled_FiresKillCountedEvent()
        {
            // Arrange
            SetupWaveState(targetKills: 10, currentKills: 0, requiredBossKills: 0, currentBossKills: 0);

            // Act
            _manager.OnEnemyKilled();

            // Assert
            Assert.That(_killCountedEvents.Count, Is.EqualTo(1));
        }

        [Test]
        public void OnEnemyKilled_WhenTargetReached_DoesNotIncrementFurther()
        {
            // Arrange - ボス要件が未達成の状態でターゲットに到達
            // これによりウェーブクリアがトリガーされず、キルカウントが維持される
            SetupWaveState(targetKills: 5, currentKills: 5, requiredBossKills: 1, currentBossKills: 0);

            // Act
            _manager.OnEnemyKilled();

            // Assert - Kill count should not exceed target (boss requirement blocks wave clear)
            Assert.That(_manager.EnemiesKilled.CurrentValue, Is.EqualTo(5));
        }

        [Test]
        public void OnEnemyKilled_WhenTargetReached_DoesNotFireKillCountedEvent()
        {
            // Arrange - ボス要件が未達成の状態でターゲットに到達
            SetupWaveState(targetKills: 5, currentKills: 5, requiredBossKills: 1, currentBossKills: 0);

            // Act
            _manager.OnEnemyKilled();

            // Assert
            Assert.That(_killCountedEvents.Count, Is.EqualTo(0));
        }

        [Test]
        public void OnEnemyKilled_BossKill_IncrementsBossKillCount()
        {
            // Arrange
            SetupWaveState(targetKills: 10, currentKills: 0, requiredBossKills: 2, currentBossKills: 0);

            // Act
            _manager.OnEnemyKilled(isBoss: true);

            // Assert
            Assert.That(_manager.BossKills.CurrentValue, Is.EqualTo(1));
        }

        [Test]
        public void OnEnemyKilled_BossKill_AlsoIncrementsRegularKillCount()
        {
            // Arrange
            SetupWaveState(targetKills: 10, currentKills: 0, requiredBossKills: 2, currentBossKills: 0);

            // Act
            _manager.OnEnemyKilled(isBoss: true);

            // Assert
            Assert.That(_manager.EnemiesKilled.CurrentValue, Is.EqualTo(1));
        }

        [Test]
        public void OnEnemyKilled_WhenTargetAndBossReached_TriggersWaveClear()
        {
            // Arrange
            SetupWaveState(targetKills: 5, currentKills: 4, requiredBossKills: 1, currentBossKills: 0);
            SetupEmptyWaves(); // Prevent StartWave from throwing

            // Act
            _manager.OnEnemyKilled(isBoss: true);

            // Assert
            Assert.That(_waveClearedEvents.Count, Is.EqualTo(1));
        }

        [Test]
        public void OnEnemyKilled_WhenTargetReachedButBossNotReached_DoesNotTriggerWaveClear()
        {
            // Arrange
            SetupWaveState(targetKills: 5, currentKills: 4, requiredBossKills: 2, currentBossKills: 0);

            // Act
            _manager.OnEnemyKilled(isBoss: false); // Regular kill reaches target

            // Assert - Wave not cleared because boss requirement not met
            Assert.That(_waveClearedEvents.Count, Is.EqualTo(0));
        }

        [Test]
        public void OnEnemyKilled_WhenBossReachedButTargetNotReached_DoesNotTriggerWaveClear()
        {
            // Arrange
            SetupWaveState(targetKills: 10, currentKills: 3, requiredBossKills: 1, currentBossKills: 0);

            // Act
            _manager.OnEnemyKilled(isBoss: true); // Boss kill but target not reached

            // Assert
            Assert.That(_waveClearedEvents.Count, Is.EqualTo(0));
        }

        [Test]
        public void OnEnemyKilled_WithZeroBossRequirement_ClearsOnTargetReached()
        {
            // Arrange
            SetupWaveState(targetKills: 5, currentKills: 4, requiredBossKills: 0, currentBossKills: 0);
            SetupEmptyWaves();

            // Act
            _manager.OnEnemyKilled(isBoss: false);

            // Assert
            Assert.That(_waveClearedEvents.Count, Is.EqualTo(1));
        }

        #endregion

        #region IsLastWave Tests

        [Test]
        public void IsLastWave_WithEmptyWaves_ReturnsFalse()
        {
            // Arrange
            SetupEmptyWaves();

            // Assert
            Assert.That(_manager.IsLastWave, Is.False);
        }

        [Test]
        public void IsLastWave_WithSingleWave_ReturnsTrue()
        {
            // Arrange
            SetupWavesArray(1);
            SetCurrentWaveIndex(0);

            // Assert
            Assert.That(_manager.IsLastWave, Is.True);
        }

        [Test]
        public void IsLastWave_WithMultipleWaves_OnLastWave_ReturnsTrue()
        {
            // Arrange
            SetupWavesArray(3);
            SetCurrentWaveIndex(2); // Last wave index

            // Assert
            Assert.That(_manager.IsLastWave, Is.True);
        }

        [Test]
        public void IsLastWave_WithMultipleWaves_NotOnLastWave_ReturnsFalse()
        {
            // Arrange
            SetupWavesArray(3);
            SetCurrentWaveIndex(0); // First wave

            // Assert
            Assert.That(_manager.IsLastWave, Is.False);
        }

        #endregion

        #region Dispose Tests

        [Test]
        public void Dispose_CanBeCalledWithoutException()
        {
            // Arrange
            var manager = new SurvivorStageWaveManager();

            // Act & Assert - Dispose should not throw
            Assert.DoesNotThrow(() => manager.Dispose());
        }

        [Test]
        public void Dispose_MultipleCallsDoNotThrow()
        {
            // Arrange
            var manager = new SurvivorStageWaveManager();

            // Act & Assert - Multiple dispose calls should be safe
            Assert.DoesNotThrow(() =>
            {
                manager.Dispose();
                manager.Dispose();
            });
        }

        #endregion

        #region Helper Methods

        private void SetupWaveState(int targetKills, int currentKills, int requiredBossKills, int currentBossKills)
        {
            // Set wave number first
            SetReactivePropertyValue("_currentWave", 1);

            // Set target and current kills
            SetReactivePropertyValue("_targetKillsThisWave", targetKills);
            SetReactivePropertyValue("_enemiesKilled", currentKills);

            // Set boss requirements
            SetReactivePropertyValue("_requiredBossKills", requiredBossKills);
            SetReactivePropertyValue("_bossKills", currentBossKills);
        }

        private void SetReactivePropertyValue<T>(string fieldName, T value)
        {
            var field = typeof(SurvivorStageWaveManager).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field?.GetValue(_manager) is ReactiveProperty<T> property)
            {
                property.Value = value;
            }
        }

        private void SetupEmptyWaves()
        {
            // Set empty waves array to prevent null reference in StartWave
            var field = typeof(SurvivorStageWaveManager).GetField("_waves", BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(_manager, new Game.Client.MasterData.SurvivorStageWaveMaster[0]);

            // Set wave index to -1 so StartWave will set IsAllWavesCleared
            var indexField = typeof(SurvivorStageWaveManager).GetField("_currentWaveIndex", BindingFlags.NonPublic | BindingFlags.Instance);
            indexField?.SetValue(_manager, -1);
        }

        private void SetupWavesArray(int count)
        {
            // Create wave array with specified count
            var waves = new Game.Client.MasterData.SurvivorStageWaveMaster[count];
            var field = typeof(SurvivorStageWaveManager).GetField("_waves", BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(_manager, waves);
        }

        private void SetCurrentWaveIndex(int index)
        {
            var indexField = typeof(SurvivorStageWaveManager).GetField("_currentWaveIndex", BindingFlags.NonPublic | BindingFlags.Instance);
            indexField?.SetValue(_manager, index);
        }

        #endregion
    }
}
