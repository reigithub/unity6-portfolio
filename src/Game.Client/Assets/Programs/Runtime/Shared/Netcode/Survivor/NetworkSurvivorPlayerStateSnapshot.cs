using Unity.Collections;
using Unity.Netcode;
using Game.Library.Shared.Dto;

namespace Game.Shared.Netcode.Survivor
{
    public struct NetworkSurvivorPlayerStateSnapshot : INetworkSerializable
    {
        public FixedString64Bytes UserId;
        public float PositionX;
        public float PositionY;
        public float PositionZ;
        public float RotationY;
        public float Speed;
        public int CurrentHp;
        public int CurrentStamina;
        public bool IsInvincible;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref UserId);
            serializer.SerializeValue(ref PositionX);
            serializer.SerializeValue(ref PositionY);
            serializer.SerializeValue(ref PositionZ);
            serializer.SerializeValue(ref RotationY);
            serializer.SerializeValue(ref Speed);
            serializer.SerializeValue(ref CurrentHp);
            serializer.SerializeValue(ref CurrentStamina);
            serializer.SerializeValue(ref IsInvincible);
        }

        public static NetworkSurvivorPlayerStateSnapshot FromDto(PlayerStateSnapshot dto)
        {
            return new NetworkSurvivorPlayerStateSnapshot
            {
                UserId = new FixedString64Bytes(dto.UserId),
                PositionX = dto.PositionX,
                PositionY = dto.PositionY,
                PositionZ = dto.PositionZ,
                RotationY = dto.RotationY,
                Speed = dto.Speed,
                CurrentHp = dto.CurrentHp,
                CurrentStamina = dto.CurrentStamina,
                IsInvincible = dto.IsInvincible,
            };
        }

        public PlayerStateSnapshot ToDto()
        {
            return new PlayerStateSnapshot
            {
                UserId = UserId.ToString(),
                PositionX = PositionX,
                PositionY = PositionY,
                PositionZ = PositionZ,
                RotationY = RotationY,
                Speed = Speed,
                CurrentHp = CurrentHp,
                CurrentStamina = CurrentStamina,
                IsInvincible = IsInvincible,
            };
        }
    }
}
