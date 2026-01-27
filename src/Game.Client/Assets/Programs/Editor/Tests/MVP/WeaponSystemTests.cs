using System;
using System.Reflection;
using Game.Library.Shared.MasterData.MemoryTables;
using Game.MVP.Survivor.Weapon;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.MVP
{
    [TestFixture]
    public class WeaponSystemTests
    {
        #region Test Weapon Implementation

        private class TestWeapon : SurvivorWeaponBase
        {
            public bool TryAttackResult { get; set; } = true;
            public int AttackCount { get; private set; }

            public TestWeapon() : base(new SurvivorWeaponMaster { Id = 1, Name = "TestWeapon" })
            {
            }

            protected override bool TryAttack()
            {
                AttackCount++;
                return TryAttackResult;
            }

            // Expose protected fields for testing
            public void SetDamageForTest(int damage) => _damage = damage;
            public void SetCooldownForTest(int cooldown) => _cooldown = cooldown;
            public void SetIntervalForTest(int interval) => _interval = interval;
            public void SetLevelForTest(int level) => _level = level;
            public void SetMaxLevelForTest(int maxLevel) => _maxLevel = maxLevel;
            public void SetCritChanceForTest(int critChance) => _critChance = critChance;
            public void SetCritMultiplierForTest(int critMultiplier) => _critMultiplier = critMultiplier;
            public void SetProcRateForTest(int procRate) => _procRate = procRate;
            public void SetOwnerForTest(Transform owner) => _owner = owner;
            public void SetCooldownTimerForTest(float timer) => _cooldownTimer = timer;
            public void SetAttackTimerForTest(float timer) => _attackTimer = timer;

            public int GetDamageInternal() => _damage;
            public float GetCooldownTimer() => _cooldownTimer;
            public float GetAttackTimer() => _attackTimer;

            // Expose protected methods for testing
            public bool TestRollCritical() => RollCritical();
            public int TestCalculateCriticalDamage(int baseDamage) => CalculateCriticalDamage(baseDamage);
            public bool TestRollProcRate() => RollProcRate();
        }

        #endregion

        private TestWeapon _weapon;
        private GameObject _ownerObject;

        [SetUp]
        public void Setup()
        {
            _weapon = new TestWeapon();
            _ownerObject = new GameObject("Owner");
            _weapon.SetOwnerForTest(_ownerObject.transform);
            _weapon.SetDamageForTest(100);
            _weapon.SetIntervalForTest(1000); // 1秒
        }

        [TearDown]
        public void TearDown()
        {
            _weapon?.Dispose();
            if (_ownerObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_ownerObject);
            }
        }

        #region Damage Tests

        [Test]
        public void Damage_WithDefaultMultiplier_ReturnsBaseDamage()
        {
            // Arrange
            _weapon.SetDamageForTest(100);

            // Assert
            Assert.That(_weapon.Damage, Is.EqualTo(100));
        }

        [Test]
        public void Damage_WithMultiplier_ReturnsScaledDamage()
        {
            // Arrange
            _weapon.SetDamageForTest(100);
            _weapon.SetDamageMultiplier(1.5f);

            // Assert
            Assert.That(_weapon.Damage, Is.EqualTo(150));
        }

        [Test]
        public void Damage_WithDoubleMultiplier_ReturnsDoubleDamage()
        {
            // Arrange
            _weapon.SetDamageForTest(100);
            _weapon.SetDamageMultiplier(2f);

            // Assert
            Assert.That(_weapon.Damage, Is.EqualTo(200));
        }

        [Test]
        public void Damage_WithHalfMultiplier_ReturnsHalfDamage()
        {
            // Arrange
            _weapon.SetDamageForTest(100);
            _weapon.SetDamageMultiplier(0.5f);

            // Assert
            Assert.That(_weapon.Damage, Is.EqualTo(50));
        }

        [Test]
        public void Damage_RoundsToNearestInteger()
        {
            // Arrange
            _weapon.SetDamageForTest(100);
            _weapon.SetDamageMultiplier(1.234f);

            // Assert
            // 100 * 1.234 = 123.4 → rounds to 123
            Assert.That(_weapon.Damage, Is.EqualTo(123));
        }

        #endregion

        #region SetEnabled Tests

        [Test]
        public void SetEnabled_DefaultIsTrue()
        {
            // Assert
            Assert.That(_weapon.IsEnabled, Is.True);
        }

        [Test]
        public void SetEnabled_False_DisablesWeapon()
        {
            // Act
            _weapon.SetEnabled(false);

            // Assert
            Assert.That(_weapon.IsEnabled, Is.False);
        }

        [Test]
        public void SetEnabled_True_EnablesWeapon()
        {
            // Arrange
            _weapon.SetEnabled(false);

            // Act
            _weapon.SetEnabled(true);

            // Assert
            Assert.That(_weapon.IsEnabled, Is.True);
        }

        #endregion

        #region IsManualActivation Tests

        [Test]
        public void IsManualActivation_WithZeroCooldown_ReturnsFalse()
        {
            // Arrange
            _weapon.SetCooldownForTest(0);

            // Assert
            Assert.That(_weapon.IsManualActivation, Is.False);
        }

        [Test]
        public void IsManualActivation_WithPositiveCooldown_ReturnsTrue()
        {
            // Arrange
            _weapon.SetCooldownForTest(1000);

            // Assert
            Assert.That(_weapon.IsManualActivation, Is.True);
        }

        #endregion

        #region IsOnCooldown Tests

        [Test]
        public void IsOnCooldown_WithZeroTimer_ReturnsFalse()
        {
            // Arrange
            _weapon.SetCooldownTimerForTest(0f);

            // Assert
            Assert.That(_weapon.IsOnCooldown, Is.False);
        }

        [Test]
        public void IsOnCooldown_WithPositiveTimer_ReturnsTrue()
        {
            // Arrange
            _weapon.SetCooldownTimerForTest(1f);

            // Assert
            Assert.That(_weapon.IsOnCooldown, Is.True);
        }

        #endregion

        #region CooldownProgress Tests

        [Test]
        public void CooldownProgress_WithNoCooldown_ReturnsOne()
        {
            // Arrange
            _weapon.SetCooldownForTest(0);

            // Assert
            Assert.That(_weapon.CooldownProgress, Is.EqualTo(1f));
        }

        [Test]
        public void CooldownProgress_WhenCooldownComplete_ReturnsOne()
        {
            // Arrange
            _weapon.SetCooldownForTest(1000); // 1秒
            _weapon.SetCooldownTimerForTest(0f);

            // Assert
            Assert.That(_weapon.CooldownProgress, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void CooldownProgress_WhenHalfwayCooldown_ReturnsHalf()
        {
            // Arrange
            _weapon.SetCooldownForTest(2000); // 2秒
            _weapon.SetCooldownTimerForTest(1f); // 1秒残り

            // Assert
            // Progress = 1 - (1 / 2) = 0.5
            Assert.That(_weapon.CooldownProgress, Is.EqualTo(0.5f).Within(0.001f));
        }

        #endregion

        #region UpdateWeapon Tests

        [Test]
        public void UpdateWeapon_WhenDisabled_DoesNotAttack()
        {
            // Arrange
            _weapon.SetEnabled(false);
            _weapon.SetAttackTimerForTest(-1f); // Should trigger attack

            // Act
            _weapon.UpdateWeapon(0.1f);

            // Assert
            Assert.That(_weapon.AttackCount, Is.EqualTo(0));
        }

        [Test]
        public void UpdateWeapon_WithNoOwner_DoesNotAttack()
        {
            // Arrange
            _weapon.SetOwnerForTest(null);
            _weapon.SetAttackTimerForTest(-1f);

            // Act
            _weapon.UpdateWeapon(0.1f);

            // Assert
            Assert.That(_weapon.AttackCount, Is.EqualTo(0));
        }

        [Test]
        public void UpdateWeapon_DecrementsCooldownTimer()
        {
            // Arrange
            _weapon.SetCooldownTimerForTest(2f);

            // Act
            _weapon.UpdateWeapon(0.5f);

            // Assert
            Assert.That(_weapon.GetCooldownTimer(), Is.EqualTo(1.5f).Within(0.001f));
        }

        [Test]
        public void UpdateWeapon_CooldownTimerClampsToZero()
        {
            // Arrange
            _weapon.SetCooldownTimerForTest(0.5f);

            // Act
            _weapon.UpdateWeapon(1f);

            // Assert
            Assert.That(_weapon.GetCooldownTimer(), Is.EqualTo(0f));
        }

        [Test]
        public void UpdateWeapon_ManualWeapon_DoesNotAutoAttack()
        {
            // Arrange
            _weapon.SetCooldownForTest(1000); // Manual weapon
            _weapon.SetAttackTimerForTest(-1f);

            // Act
            _weapon.UpdateWeapon(0.1f);

            // Assert
            Assert.That(_weapon.AttackCount, Is.EqualTo(0));
        }

        [Test]
        public void UpdateWeapon_AutoWeapon_AttacksWhenTimerExpires()
        {
            // Arrange
            _weapon.SetCooldownForTest(0); // Auto weapon
            _weapon.SetAttackTimerForTest(0.1f);

            // Act
            _weapon.UpdateWeapon(0.2f); // Timer goes negative

            // Assert
            Assert.That(_weapon.AttackCount, Is.EqualTo(1));
        }

        [Test]
        public void UpdateWeapon_AutoWeapon_ResetsTimerAfterAttack()
        {
            // Arrange
            _weapon.SetCooldownForTest(0);
            _weapon.SetIntervalForTest(1000); // 1秒
            _weapon.SetAttackTimerForTest(-0.1f);

            // Act
            _weapon.UpdateWeapon(0.1f);

            // Assert
            Assert.That(_weapon.GetAttackTimer(), Is.EqualTo(1f).Within(0.001f));
        }

        #endregion

        #region TryManualActivate Tests

        [Test]
        public void TryManualActivate_NonManualWeapon_ReturnsFalse()
        {
            // Arrange
            _weapon.SetCooldownForTest(0);

            // Act
            var result = _weapon.TryManualActivate();

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void TryManualActivate_OnCooldown_ReturnsFalse()
        {
            // Arrange
            _weapon.SetCooldownForTest(1000);
            _weapon.SetCooldownTimerForTest(0.5f);

            // Act
            var result = _weapon.TryManualActivate();

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void TryManualActivate_WhenDisabled_ReturnsFalse()
        {
            // Arrange
            _weapon.SetCooldownForTest(1000);
            _weapon.SetEnabled(false);

            // Act
            var result = _weapon.TryManualActivate();

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void TryManualActivate_Success_ReturnsTrue()
        {
            // Arrange
            _weapon.SetCooldownForTest(1000);
            _weapon.SetCooldownTimerForTest(0f);
            _weapon.TryAttackResult = true;

            // Act
            var result = _weapon.TryManualActivate();

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void TryManualActivate_Success_SetsCooldownTimer()
        {
            // Arrange
            _weapon.SetCooldownForTest(2000); // 2秒
            _weapon.SetCooldownTimerForTest(0f);
            _weapon.TryAttackResult = true;

            // Act
            _weapon.TryManualActivate();

            // Assert
            Assert.That(_weapon.GetCooldownTimer(), Is.EqualTo(2f).Within(0.001f));
        }

        [Test]
        public void TryManualActivate_AttackFails_ReturnsFalse()
        {
            // Arrange
            _weapon.SetCooldownForTest(1000);
            _weapon.TryAttackResult = false;

            // Act
            var result = _weapon.TryManualActivate();

            // Assert
            Assert.That(result, Is.False);
        }

        #endregion

        #region Critical Tests

        [Test]
        public void RollCritical_WithZeroChance_ReturnsFalse()
        {
            // Arrange
            _weapon.SetCritChanceForTest(0);

            // Act & Assert
            Assert.That(_weapon.TestRollCritical(), Is.False);
        }

        [Test]
        public void CalculateCriticalDamage_WithDefaultMultiplier_Returns150Percent()
        {
            // Arrange
            _weapon.SetCritMultiplierForTest(15000); // 150% in 万分率

            // Act
            var critDamage = _weapon.TestCalculateCriticalDamage(100);

            // Assert
            Assert.That(critDamage, Is.EqualTo(150));
        }

        [Test]
        public void CalculateCriticalDamage_WithDoubleMultiplier_Returns200Percent()
        {
            // Arrange
            _weapon.SetCritMultiplierForTest(20000); // 200% in 万分率

            // Act
            var critDamage = _weapon.TestCalculateCriticalDamage(100);

            // Assert
            Assert.That(critDamage, Is.EqualTo(200));
        }

        #endregion

        #region ProcRate Tests

        [Test]
        public void RollProcRate_WithZeroRate_ReturnsFalse()
        {
            // Arrange
            _weapon.SetProcRateForTest(0);

            // Act & Assert
            Assert.That(_weapon.TestRollProcRate(), Is.False);
        }

        [Test]
        public void RollProcRate_WithMaxRate_ReturnsTrue()
        {
            // Arrange
            _weapon.SetProcRateForTest(10000); // 100%

            // Act & Assert
            Assert.That(_weapon.TestRollProcRate(), Is.True);
        }

        #endregion

        #region LevelUp Tests

        [Test]
        public void LevelUp_AtMaxLevel_ReturnsFalse()
        {
            // Arrange
            _weapon.SetLevelForTest(10);
            _weapon.SetMaxLevelForTest(10);

            // Act
            var result = _weapon.LevelUp();

            // Assert
            Assert.That(result, Is.False);
        }

        #endregion

        #region Dispose Tests

        [Test]
        public void Dispose_DisposesSubjects()
        {
            // Arrange
            var weapon = new TestWeapon();

            // Act
            weapon.Dispose();

            // Assert - Disposing twice should not throw
            Assert.DoesNotThrow(() => weapon.Dispose());
        }

        #endregion
    }
}
