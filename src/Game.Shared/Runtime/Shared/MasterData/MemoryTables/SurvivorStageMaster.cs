using MasterMemory;
using MessagePack;

namespace Game.Library.Shared.MasterData.MemoryTables
{
    /// <summary>
    /// Survivorステージマスター
    /// ステージの基本設定を定義
    /// </summary>
    [MemoryTable("SurvivorStageMaster"), MessagePackObject(true)]
    public sealed partial class SurvivorStageMaster
    {
        [PrimaryKey]
        public int Id { get; set; }

        public string Name { get; set; }

        public string AssetName { get; set; }

        public string ThumbnailAssetName { get; set; }

        /// <summary>ステージの説明</summary>
        public string Description { get; set; }

        /// <summary>制限時間（秒）</summary>
        public int TimeLimit { get; set; }

        /// <summary>使用するプレイヤーID</summary>
        public int PlayerId { get; set; }

        /// <summary>BGMアセット名</summary>
        public string BgmAssetName { get; set; }

        /// <summary>解放条件（前提ステージID、null=最初から解放）</summary>
        public int? UnlockStageId { get; set; }

        /// <summary>難易度（1:Easy, 2:Normal, 3:Hard）</summary>
        public int Difficulty { get; set; }
    }
}
