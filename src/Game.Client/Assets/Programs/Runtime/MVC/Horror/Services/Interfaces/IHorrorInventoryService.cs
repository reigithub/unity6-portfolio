using System.Collections.Generic;
using Game.Horror.SaveData;
using Game.Shared.Enums;
using Game.Shared.Services.Interfaces;

namespace Game.Horror.Services.Interfaces
{
    /// <summary>
    /// Horror インベントリ（所持アイテム）を扱うドメインサービスのインターフェース。
    /// </summary>
    public interface IHorrorInventoryService : IGameService
    {
        /// <summary>所持アイテム一覧（読み取り専用、追加順）。未ロード時は空。</summary>
        IReadOnlyList<HorrorInventorySlotData> Slots { get; }

        /// <summary>
        /// アイテムをインベントリに追加する。既存スタックを先頭から maxCount まで充填し、
        /// 超過分は新規スロットへ分割して格納する。
        /// 全量が入らない場合（空きスロットも尽きる場合）は何もせず false（全量成功 or 完全失敗）。
        /// </summary>
        bool TryAdd(ObjectCategory category, int id, int addCount, int maxCount);

        /// <summary>指定 (SlotType, Id) を所持しているか判定する。</summary>
        bool HasObject(ObjectCategory category, int id);

        /// <summary>指定 (SlotType, Id) の所持数を取得する。複数スロットに分割されている場合は合算。未所持は 0。</summary>
        int GetCount(ObjectCategory category, int id);

        /// <summary>
        /// 指定数を消費する。複数スロットにまたがる場合は先頭のスロットから順に消費する。
        /// 合計所持数が不足なら何もせず false（部分消費しない）。
        /// </summary>
        bool TryConsume(ObjectCategory category, int id, int count);

        /// <summary>指定位置のスロットを丸ごと破棄する。範囲外の index は何もせず false。</summary>
        bool DiscardSlot(int slotIndex);
    }
}
