using MasterMemory;
using MessagePack;

namespace Game.Library.Shared.MasterData.MemoryTables
{
    /// <summary>
    /// Survivorプレイヤーマスター
    /// プレイヤーキャラクターの基本情報を定義
    /// レベル依存のパラメータはSurvivorPlayerLevelMasterを参照
    /// </summary>
    [MemoryTable("SurvivorPlayerMaster"), MessagePackObject(true)]
    public sealed partial class SurvivorPlayerMaster
    {
        [PrimaryKey]
        public int Id { get; set; }

        public string Name { get; set; }

        public string AssetName { get; set; }

        /// <summary>初期武器ID</summary>
        public int StartingWeaponId { get; set; }
    }
}
