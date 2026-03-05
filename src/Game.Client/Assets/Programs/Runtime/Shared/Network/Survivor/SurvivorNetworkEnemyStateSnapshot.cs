using Game.Library.Shared.Dto;

namespace Game.Shared.Network.Survivor
{
    public struct SurvivorNetworkEnemyStateSnapshot
    {
        public int NetworkId;
        public int EnemyMasterId;
        public float PositionX;
        public float PositionZ;
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
                PositionZ = dto.PositionZ,
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
                PositionZ = PositionZ,
                CurrentHp = CurrentHp,
                SyncType = SyncType,
            };
        }
    }
}
