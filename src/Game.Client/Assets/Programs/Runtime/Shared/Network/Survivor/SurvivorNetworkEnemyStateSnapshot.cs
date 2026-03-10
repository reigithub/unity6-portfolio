using Game.Library.Shared.Dto;

namespace Game.Shared.Network.Survivor
{
    public struct SurvivorNetworkEnemyStateSnapshot
    {
        public int NetworkId;
        public int EnemyMasterId;
        public float PositionX;
        public float PositionY;
        public float PositionZ;
        public float VelocityX;
        public float VelocityY;
        public float VelocityZ;
        public int CurrentHp;
        public byte SyncTypeByte;

        public EnemySyncType SyncType
        {
            get { return (EnemySyncType)SyncTypeByte; }
            set { SyncTypeByte = (byte)value; }
        }

        public static SurvivorNetworkEnemyStateSnapshot FromDto(EnemyStateSnapshot dto)
        {
            return new SurvivorNetworkEnemyStateSnapshot
            {
                NetworkId = dto.NetworkId,
                EnemyMasterId = dto.EnemyMasterId,
                PositionX = dto.PositionX,
                PositionY = dto.PositionY,
                PositionZ = dto.PositionZ,
                VelocityX = dto.VelocityX,
                VelocityY = dto.VelocityY,
                VelocityZ = dto.VelocityZ,
                CurrentHp = dto.CurrentHp,
                SyncType = dto.SyncType,
            };
        }

        public EnemyStateSnapshot ToDto()
        {
            return new EnemyStateSnapshot
            {
                NetworkId = NetworkId,
                EnemyMasterId = EnemyMasterId,
                PositionX = PositionX,
                PositionY = PositionY,
                PositionZ = PositionZ,
                VelocityX = VelocityX,
                VelocityY = VelocityY,
                VelocityZ = VelocityZ,
                CurrentHp = CurrentHp,
                SyncType = SyncType,
            };
        }
    }
}
