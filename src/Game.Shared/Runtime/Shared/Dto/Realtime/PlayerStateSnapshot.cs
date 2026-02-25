using MessagePack;

namespace Game.Library.Shared.Dto
{
    /// <summary>
    /// プレイヤー状態スナップショット（20Hz同期用）
    /// </summary>
    [MessagePackObject]
    public class PlayerStateSnapshot
    {
        [Key(0)]
        public string UserId { get; set; } = string.Empty;

        [Key(1)]
        public float PositionX { get; set; }

        [Key(2)]
        public float PositionY { get; set; }

        [Key(3)]
        public float PositionZ { get; set; }

        [Key(4)]
        public float RotationY { get; set; }

        [Key(5)]
        public float Speed { get; set; }

        [Key(6)]
        public int CurrentHp { get; set; }

        [Key(7)]
        public int CurrentStamina { get; set; }

        [Key(8)]
        public bool IsInvincible { get; set; }
    }
}
