using Game.Library.Shared.Dto;
using Game.Shared.Network.Survivor;
using NUnit.Framework;

namespace Game.Tests.Shared.Network
{
    [TestFixture]
    public class SurvivorNetworkEnemyStateSnapshotTests
    {
        #region FromDto Tests

        [Test]
        public void FromDto_MapsAllFields_IncludingVelocity()
        {
            // Arrange
            var dto = new EnemyStateSnapshot
            {
                NetworkId = 42,
                EnemyMasterId = 7,
                PositionX = 1.5f,
                PositionY = 2.5f,
                PositionZ = 3.5f,
                VelocityX = 4.0f,
                VelocityY = 5.0f,
                VelocityZ = 6.0f,
                CurrentHp = 100,
                SyncType = EnemySyncType.PositionUpdate,
            };

            // Act
            var snapshot = SurvivorNetworkEnemyStateSnapshot.FromDto(dto);

            // Assert
            Assert.That(snapshot.NetworkId, Is.EqualTo(42));
            Assert.That(snapshot.EnemyMasterId, Is.EqualTo(7));
            Assert.That(snapshot.PositionX, Is.EqualTo(1.5f));
            Assert.That(snapshot.PositionY, Is.EqualTo(2.5f));
            Assert.That(snapshot.PositionZ, Is.EqualTo(3.5f));
            Assert.That(snapshot.VelocityX, Is.EqualTo(4.0f));
            Assert.That(snapshot.VelocityY, Is.EqualTo(5.0f));
            Assert.That(snapshot.VelocityZ, Is.EqualTo(6.0f));
            Assert.That(snapshot.CurrentHp, Is.EqualTo(100));
            Assert.That(snapshot.SyncType, Is.EqualTo(EnemySyncType.PositionUpdate));
        }

        [Test]
        public void FromDto_ZeroVelocity_MapsCorrectly()
        {
            // Arrange
            var dto = new EnemyStateSnapshot
            {
                NetworkId = 1,
                VelocityX = 0f,
                VelocityY = 0f,
                VelocityZ = 0f,
                SyncType = EnemySyncType.Spawn,
            };

            // Act
            var snapshot = SurvivorNetworkEnemyStateSnapshot.FromDto(dto);

            // Assert
            Assert.That(snapshot.VelocityX, Is.EqualTo(0f));
            Assert.That(snapshot.VelocityY, Is.EqualTo(0f));
            Assert.That(snapshot.VelocityZ, Is.EqualTo(0f));
        }

        [Test]
        public void FromDto_NegativeVelocity_MapsCorrectly()
        {
            // Arrange
            var dto = new EnemyStateSnapshot
            {
                NetworkId = 1,
                VelocityX = -3.5f,
                VelocityY = -1.2f,
                VelocityZ = -7.8f,
            };

            // Act
            var snapshot = SurvivorNetworkEnemyStateSnapshot.FromDto(dto);

            // Assert
            Assert.That(snapshot.VelocityX, Is.EqualTo(-3.5f));
            Assert.That(snapshot.VelocityY, Is.EqualTo(-1.2f));
            Assert.That(snapshot.VelocityZ, Is.EqualTo(-7.8f));
        }

        #endregion

        #region ToDto Tests

        [Test]
        public void ToDto_MapsAllFields_IncludingVelocity()
        {
            // Arrange
            var snapshot = new SurvivorNetworkEnemyStateSnapshot
            {
                NetworkId = 99,
                EnemyMasterId = 3,
                PositionX = 10f,
                PositionY = 20f,
                PositionZ = 30f,
                VelocityX = -1.5f,
                VelocityY = 2.5f,
                VelocityZ = -3.5f,
                CurrentHp = 50,
                SyncType = EnemySyncType.Death,
            };

            // Act
            var dto = snapshot.ToDto();

            // Assert
            Assert.That(dto.NetworkId, Is.EqualTo(99));
            Assert.That(dto.EnemyMasterId, Is.EqualTo(3));
            Assert.That(dto.PositionX, Is.EqualTo(10f));
            Assert.That(dto.PositionY, Is.EqualTo(20f));
            Assert.That(dto.PositionZ, Is.EqualTo(30f));
            Assert.That(dto.VelocityX, Is.EqualTo(-1.5f));
            Assert.That(dto.VelocityY, Is.EqualTo(2.5f));
            Assert.That(dto.VelocityZ, Is.EqualTo(-3.5f));
            Assert.That(dto.CurrentHp, Is.EqualTo(50));
            Assert.That(dto.SyncType, Is.EqualTo(EnemySyncType.Death));
        }

        #endregion

        #region RoundTrip Tests

        [Test]
        public void RoundTrip_DtoToStructToDto_PreservesAllValues()
        {
            // Arrange
            var original = new EnemyStateSnapshot
            {
                NetworkId = 123,
                EnemyMasterId = 45,
                PositionX = 11.1f,
                PositionY = 22.2f,
                PositionZ = 33.3f,
                VelocityX = 44.4f,
                VelocityY = 55.5f,
                VelocityZ = 66.6f,
                CurrentHp = 200,
                SyncType = EnemySyncType.PositionUpdate,
            };

            // Act
            var roundTripped = SurvivorNetworkEnemyStateSnapshot.FromDto(original).ToDto();

            // Assert
            Assert.That(roundTripped.NetworkId, Is.EqualTo(original.NetworkId));
            Assert.That(roundTripped.EnemyMasterId, Is.EqualTo(original.EnemyMasterId));
            Assert.That(roundTripped.PositionX, Is.EqualTo(original.PositionX));
            Assert.That(roundTripped.PositionY, Is.EqualTo(original.PositionY));
            Assert.That(roundTripped.PositionZ, Is.EqualTo(original.PositionZ));
            Assert.That(roundTripped.VelocityX, Is.EqualTo(original.VelocityX));
            Assert.That(roundTripped.VelocityY, Is.EqualTo(original.VelocityY));
            Assert.That(roundTripped.VelocityZ, Is.EqualTo(original.VelocityZ));
            Assert.That(roundTripped.CurrentHp, Is.EqualTo(original.CurrentHp));
            Assert.That(roundTripped.SyncType, Is.EqualTo(original.SyncType));
        }

        #endregion

        #region SyncType Tests

        [Test]
        [TestCase(EnemySyncType.Spawn)]
        [TestCase(EnemySyncType.PositionUpdate)]
        [TestCase(EnemySyncType.Death)]
        public void SyncType_GetSet_ConvertsCorrectly(EnemySyncType syncType)
        {
            // Arrange
            var snapshot = new SurvivorNetworkEnemyStateSnapshot();

            // Act
            snapshot.SyncType = syncType;

            // Assert
            Assert.That(snapshot.SyncType, Is.EqualTo(syncType));
            Assert.That(snapshot.SyncTypeByte, Is.EqualTo((byte)syncType));
        }

        #endregion
    }
}
