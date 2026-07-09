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
        public int Version { get; set; } = 1;

        public HorrorPlayerSaveData Player { get; set; } = new();

        public HorrorInventorySaveData Inventory { get; set; } = new();

        public HorrorEquipmentSaveData Equipment { get; set; } = new();

        public HorrorInteractionSaveData Interaction { get; set; } = new();
    }
}
