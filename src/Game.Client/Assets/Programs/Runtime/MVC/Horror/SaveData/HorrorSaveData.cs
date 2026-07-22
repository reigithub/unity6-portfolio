using System;
using Game.Horror.Constants;
using MemoryPack;

namespace Game.Horror.SaveData
{
    /// <summary>
    /// Horror ゲームのセーブデータルート。プレイヤー・インベントリ・装備・インタラクション記録を
    /// セクションごとに区画化して保持する（オプション設定は別セーブ扱いのため含まない）。
    /// </summary>
    [MemoryPackable]
    public partial class HorrorSaveData
    {
        /// <summary>セーブデータバージョン（マイグレーション用）</summary>
        public int Version { get; set; } = HorrorSaveConstants.SaveDataLatestVersion;

        /// <summary>保存先スロット番号（-1 = 未保存）</summary>
        public int SlotNo { get; set; } = -1;

        /// <summary>保存日時（UTC）</summary>
        public DateTime SavedAtUtc { get; set; }

        /// <summary>保存時点のセーブポイント Id（HorrorInteractionMaster の Id、0 = なし）</summary>
        public int SavepointId { get; set; }

        public HorrorPlayerSaveData Player { get; set; } = new();

        public HorrorInteractionSaveData Interaction { get; set; } = new();

        public HorrorInventorySaveData Inventory { get; set; } = new();

        public HorrorEquipmentSaveData Equipment { get; set; } = new();

        public HorrorKeyItemSaveData KeyItem { get; set; } = new();
    }
}
