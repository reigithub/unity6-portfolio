using System.Collections.Generic;
using Game.Core.Services;
using Game.Horror.SaveData;
using Game.Shared.Enums;
using Game.Shared.Interfaces;

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
        /// アイテムをインベントリに追加する。同一 Id が既に存在する場合はスタック加算し MaxCount で頭打ちする。
        /// </summary>
        bool TryAdd(IHorrorInventorySlotInfo info, int addCount);

        /// <summary>指定 (SlotType, Id) を所持しているか判定する。</summary>
        bool HasItem(InventorySlotType type, int id);

        /// <summary>指定 (SlotType, Id) の所持数を取得する。未所持は 0。</summary>
        int GetCount(InventorySlotType type, int id);

        /// <summary>指定数を消費する。所持数不足なら何もせず false（部分消費しない）。</summary>
        bool TryConsume(InventorySlotType type, int id, int count);
    }
}
