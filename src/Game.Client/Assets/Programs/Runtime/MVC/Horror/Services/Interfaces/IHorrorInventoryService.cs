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
        /// <summary>所持アイテム一覧（読み取り専用、疎。位置は各行の SlotNo が持ち、並び順に意味はない）。未ロード時は空。</summary>
        IReadOnlyList<HorrorInventorySlotData> Slots { get; }

        /// <summary>
        /// アイテムをインベントリに追加する。既存スタックを SlotNo 昇順に maxCount まで充填し、
        /// 超過分は最小の空き位置から新規スタックとして分割配置する。
        /// 全量が入らない場合（空き位置も尽きる場合）は何もせず false（全量成功 or 完全失敗）。
        /// </summary>
        bool TryAdd(ObjectCategory category, int id, int addCount, int maxCount);

        /// <summary>指定 (SlotType, Id) を所持しているか判定する。</summary>
        bool HasObject(ObjectCategory category, int id);

        /// <summary>指定 (SlotType, Id) の所持数を取得する。複数スロットに分割されている場合は合算。未所持は 0。</summary>
        int GetCount(ObjectCategory category, int id);

        /// <summary>
        /// 指定数を消費する。複数スロットにまたがる場合は SlotNo 昇順（画面の若い位置）から消費し、
        /// 0 になった行は除去する（他行の位置は動かない）。
        /// 合計所持数が不足なら何もせず false（部分消費しない）。
        /// </summary>
        bool TryConsume(ObjectCategory category, int id, int count);

        /// <summary>指定位置（SlotNo）のスロットを丸ごと破棄する。範囲外・空位置は何もせず false。</summary>
        bool DiscardSlot(int slotIndex);
    }
}
