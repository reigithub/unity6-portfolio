using Unity.Netcode;
using Game.Library.Shared.Dto;

namespace Game.Shared.Netcode
{
    public struct NetworkEnemyStateSnapshot : INetworkSerializable
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

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref NetworkId);
            serializer.SerializeValue(ref EnemyMasterId);
            serializer.SerializeValue(ref PositionX);
            serializer.SerializeValue(ref PositionZ);
            serializer.SerializeValue(ref CurrentHp);
            serializer.SerializeValue(ref SyncTypeByte);
        }

        public static NetworkEnemyStateSnapshot FromDto(EnemyStateSnapshot dto)
        {
            return new NetworkEnemyStateSnapshot
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
