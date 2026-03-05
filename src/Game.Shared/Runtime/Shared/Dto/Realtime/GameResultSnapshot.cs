using MessagePack;

namespace Game.Library.Shared.Dto
{
    /// <summary>
    /// ゲーム結果スナップショット
    /// </summary>
    [MessagePackObject]
    public class GameResultSnapshot
    {
        [Key(0)]
        public bool IsVictory { get; set; }

        [Key(1)]
        public float ClearTime { get; set; }

        [Key(2)]
        public PlayerResultSnapshot[] Players { get; set; }
            = System.Array.Empty<PlayerResultSnapshot>();
    }

    /// <summary>
    /// プレイヤー個別結果スナップショット
    /// </summary>
    [MessagePackObject]
    public class PlayerResultSnapshot
    {
        [Key(0)]
        public string UserId { get; set; } = string.Empty;

        [Key(1)]
        public int Score { get; set; }

        [Key(2)]
        public int TotalKills { get; set; }

        [Key(3)]
        public int Level { get; set; }
    }
}
