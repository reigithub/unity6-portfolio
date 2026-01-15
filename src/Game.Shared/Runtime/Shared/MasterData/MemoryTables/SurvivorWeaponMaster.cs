using MasterMemory;
using MessagePack;

namespace Game.Library.Shared.MasterData.MemoryTables
{
    /// <summary>
    /// Survivor武器マスター
    /// 武器の基本情報を定義
    /// </summary>
    [MemoryTable("SurvivorWeaponMaster"), MessagePackObject(true)]
    public sealed partial class SurvivorWeaponMaster
    {
        [PrimaryKey]
        public int Id { get; set; }

        public string Name { get; set; }

        public string AssetName { get; set; }

        public string IconAssetName { get; set; }

        /// <summary>武器タイプ（1:近接, 2:遠距離, 3:範囲, 4:パッシブ）</summary>
        [SecondaryKey(0), NonUnique]
        public int WeaponType { get; set; }

        /// <summary>武器の説明</summary>
        public string Description { get; set; }

        /// <summary>最大レベル</summary>
        public int MaxLevel { get; set; }

        /// <summary>レアリティ（1:コモン, 2:レア, 3:エピック, 4:レジェンド）</summary>
        public int Rarity { get; set; }
    }
}
