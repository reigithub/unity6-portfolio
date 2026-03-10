using MessagePack;

namespace Game.Library.Shared.Dto
{
    /// <summary>
    /// 敵同期タイプ
    /// </summary>
    public enum EnemySyncType : byte
    {
        Spawn = 0,
        PositionUpdate = 1,
        Death = 2,
    }

    /// <summary>
    /// 敵状態スナップショット（差分同期用）
    /// </summary>
    [MessagePackObject]
    public class EnemyStateSnapshot
    {
        [Key(0)]
        public int NetworkId { get; set; }

        [Key(1)]
        public int EnemyMasterId { get; set; }

        [Key(2)]
        public float PositionX { get; set; }

        [Key(3)]
        public float PositionY { get; set; }

        [Key(4)]
        public float PositionZ { get; set; }

        [Key(5)]
        public int CurrentHp { get; set; }

        [Key(6)]
        public EnemySyncType SyncType { get; set; }

        [Key(7)]
        public float VelocityX { get; set; }

        [Key(8)]
        public float VelocityY { get; set; }

        [Key(9)]
        public float VelocityZ { get; set; }
    }
}
