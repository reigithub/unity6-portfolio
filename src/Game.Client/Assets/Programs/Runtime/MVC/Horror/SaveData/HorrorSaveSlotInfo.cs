using System;

namespace Game.Horror.SaveData
{
    /// <summary>
    /// セーブスロット一覧表示用の読み取りモデル。永続化はしない。
    /// </summary>
    public class HorrorSaveSlotInfo
    {
        /// <summary>スロット番号。</summary>
        public int SlotNo { get; set; }

        /// <summary>データが存在するか。</summary>
        public bool HasData { get; set; }

        /// <summary>保存日時（UTC）。</summary>
        public DateTime SavedAtUtc { get; set; }

        /// <summary>保存時点のセーブポイント Id（HorrorInteractionMaster の Id、0 = なし）。</summary>
        public int SavepointId { get; set; }
    }
}
