using MasterMemory;
using MessagePack;

namespace Game.Library.Shared.MasterData.MemoryTables
{
    /// <summary>
    /// Survivor武器マスター
    /// 武器の基本定義
    /// 詳細パラメータはSurvivorWeaponLevelMasterを参照
    /// </summary>
    [MemoryTable("SurvivorWeaponMaster"), MessagePackObject(true)]
    public sealed partial class SurvivorWeaponMaster
    {
        [PrimaryKey]
        public int Id { get; set; }

        /// <summary>武器タイプID（SurvivorWeaponType enumを参照）</summary>
        [SecondaryKey(0), NonUnique]
        public int WeaponType { get; set; }

        /// <summary>レアリティ（1:コモン, 2:レア, 3:エピック, 4:レジェンド）</summary>
        public int Rarity { get; set; }

        /// <summary>武器名</summary>
        public string Name { get; set; }

        /// <summary>アイコンアセット名</summary>
        public string IconAssetName { get; set; }

        /// <summary>武器の説明（大まかな説明）</summary>
        public string Description { get; set; }
    }
}