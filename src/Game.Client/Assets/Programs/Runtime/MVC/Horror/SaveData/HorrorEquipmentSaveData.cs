using System.Collections.Generic;
using Game.Shared.Enums;
using MemoryPack;

namespace Game.Horror.SaveData
{
    /// <summary>
    /// Horror 装備状態のセーブデータ
    /// 装備中武器＋ショートカット4枠(D-Pad 1〜4)を保持する。登録内容は (SlotType, Id) のみで、
    /// 静的属性（ダメージ・アイコン等）はマスターから引く。未装備/未登録は <see cref="ObjectCategory"/> が None。
    /// </summary>
    [MemoryPackable]
    public partial class HorrorEquipmentSaveData
    {
        /// <summary>装備中のスロット種別。未装備は None。</summary>
        public ObjectCategory ObjectCategory { get; set; }

        /// <summary>装備中の Id（マスター PrimaryKey）。未装備時は無効（0）。</summary>
        public int Id { get; set; }

        /// <summary>
        /// ショートカットスロット（index 0-3 ↔ 番号 1-4）。スロット数はサービスが 4 に整える。
        /// </summary>
        public List<HorrorEquipmentSlotData> Slots { get; set; } = new();

        /// <summary>武器ごとの弾倉残弾（AmmoItemId>0 の武器のみ記録。未記録は満タン扱い）。</summary>
        public List<HorrorWeaponMagazineData> Magazines { get; set; } = new();
    }

    /// <summary>ショートカット1枠分の保存レコード。空スロットは <see cref="ObjectCategory"/> が None。</summary>
    [MemoryPackable]
    public partial class HorrorEquipmentSlotData
    {
        public ObjectCategory ObjectCategory { get; set; }

        public int Id { get; set; }
    }

    /// <summary>武器1丁分の弾倉残弾レコード。</summary>
    [MemoryPackable]
    public partial class HorrorWeaponMagazineData
    {
        public int WeaponId { get; set; }

        public int Count { get; set; }
    }
}
