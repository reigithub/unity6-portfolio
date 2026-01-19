using MasterMemory;
using MessagePack;

namespace Game.Library.Shared.MasterData.MemoryTables
{
    /// <summary>
    /// Survivorアイテムマスター
    /// ドロップアイテムの定義
    /// </summary>
    [MemoryTable("SurvivorItemMaster"), MessagePackObject(true)]
    public sealed partial class SurvivorItemMaster
    {
        [PrimaryKey]
        public int Id { get; set; }

        public string Name { get; set; }

        public string AssetName { get; set; }

        /// <summary>
        /// アイテムタイプ
        /// 1:経験値, 2:回復, 3:磁石, 4:爆弾, 5:通貨, 6:スピード, 7:時間, 8:シールド, 9:バッテリー, 10:鍵, 11:特殊
        /// </summary>
        [SecondaryKey(0), NonUnique]
        public int ItemType { get; set; }

        /// <summary>効果値（経験値量、回復量など）</summary>
        public int EffectValue { get; set; }

        /// <summary>効果範囲（磁石、爆弾用）</summary>
        public int EffectRange { get; set; }

        /// <summary>効果継続時間（ミリ秒）</summary>
        public int EffectDuration { get; set; }

        /// <summary>レアリティ（1:コモン, 2:レア, 3:エピック）</summary>
        public int Rarity { get; set; }

        /// <summary>スポーン時のスケール（1.0 = 等倍）</summary>
        public float Scale { get; set; }
    }
}
