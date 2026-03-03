using Unity.Collections;
using Game.Library.Shared.Dto;

namespace Game.Shared.Network.Survivor
{
    public struct SurvivorNetworkPlayerStateSnapshot
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

        public static SurvivorNetworkPlayerStateSnapshot FromDto(PlayerStateSnapshot dto)
        {
            return new SurvivorNetworkPlayerStateSnapshot
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
