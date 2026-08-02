using System;
using System.Collections.Generic;
using Game.Horror.Constants;
using Game.Horror.SaveData;
using Game.Horror.Services.Interfaces;
using Game.Shared.Enums;
using UnityEngine;

namespace Game.Horror.Services
{
    /// <summary>
    /// Horror インベントリ（所持アイテム）を扱うドメインサービス。
    /// 同一 (ObjectCategory, Id) のアイテムはスタック上限（maxCount）を超えると複数スロットに分割して保持する。
    /// </summary>
    public class HorrorInventoryService : IHorrorInventoryService
    {
        private const int MaxSlotCount = HorrorInventoryConstants.MaxSlotCount;

        /// <summary>所持アイテム一覧（読み取り専用、追加順）。未ロード時は空。</summary>
        public IReadOnlyList<HorrorInventorySlotData> Slots => _repository.Data?.Inventory?.Slots ?? _emptySlots;

        private readonly IReadOnlyList<HorrorInventorySlotData> _emptySlots = Array.Empty<HorrorInventorySlotData>();

        private readonly IHorrorSaveRepository _repository;

        public HorrorInventoryService(IHorrorSaveRepository repository)
        {
            _repository = repository;
        }

        /// <summary>
        /// アイテムをインベントリに追加する。既存スタックを先頭から maxCount まで充填し、
        /// 超過分は新規スロットへ分割して格納する。
        /// 全量が入らない場合（空きスロットも尽きる場合）は何もせず false（全量成功 or 完全失敗）。
        /// </summary>
        public bool TryAdd(ObjectCategory category, int id, int addCount, int maxCount)
        {
            var data = _repository.Data?.Inventory;
            if (data == null || addCount <= 0 || maxCount <= 0)
                return false;

            // 全量が入るか事前判定する（既存スタックの空き + 空きスロット × maxCount）。
            // 旧セーブがスロット数超過・MaxCount 超過 Count を持っていても負値にならないよう防御する
            long capacity = (long)Mathf.Max(0, MaxSlotCount - data.Slots.Count) * maxCount;
            foreach (var slot in data.Slots)
            {
                if (slot.ObjectCategory == category && slot.Id == id)
                    capacity += Mathf.Max(0, maxCount - slot.Count);
            }

            if (capacity < addCount)
                return false;

            // 既存スタックを先頭から充填する
            int remaining = addCount;
            foreach (var slot in data.Slots)
            {
                if (remaining <= 0)
                    break;

                if (slot.ObjectCategory != category || slot.Id != id)
                    continue;

                int fill = Mathf.Min(maxCount - slot.Count, remaining);
                if (fill <= 0)
                    continue;

                slot.Count += fill;
                remaining -= fill;
            }

            // 残量は新規スロットを末尾に追加して充填する
            while (remaining > 0)
            {
                int fill = Mathf.Min(maxCount, remaining);
                data.Slots.Add(new HorrorInventorySlotData
                {
                    ObjectCategory = category,
                    Id = id,
                    Count = fill
                });
                remaining -= fill;
            }

            _repository.MarkDirty();
            return true;
        }

        /// <summary>指定オブジェクトを所持しているか判定する。</summary>
        public bool HasObject(ObjectCategory category, int id)
            => GetCount(category, id) > 0;

        /// <summary>指定オブジェクトの所持数を取得する。複数スロットに分割されている場合は合算。未所持は 0。</summary>
        public int GetCount(ObjectCategory category, int id)
        {
            var data = _repository.Data?.Inventory;
            if (data == null)
                return 0;

            int total = 0;
            foreach (var slot in data.Slots)
            {
                if (slot.ObjectCategory == category && slot.Id == id)
                    total += slot.Count;
            }

            return total;
        }

        /// <summary>
        /// 指定数を消費する。複数スロットにまたがる場合は先頭のスロットから順に消費し、
        /// 0 になったスロットは除去する。合計所持数が不足なら何もせず false（部分消費しない）。
        /// </summary>
        public bool TryConsume(ObjectCategory category, int id, int count)
        {
            var data = _repository.Data?.Inventory;
            if (data == null || count <= 0)
                return false;

            if (GetCount(category, id) < count)
                return false;

            int remaining = count;
            foreach (var slot in data.Slots)
            {
                if (slot.ObjectCategory != category || slot.Id != id)
                    continue;

                int take = Mathf.Min(slot.Count, remaining);
                slot.Count -= take;
                remaining -= take;

                if (remaining <= 0)
                    break;
            }

            data.Slots.RemoveAll(s => s.ObjectCategory == category && s.Id == id && s.Count <= 0);

            _repository.MarkDirty();
            return true;
        }

        /// <summary>指定位置のスロットを丸ごと破棄する。範囲外の index は何もせず false。</summary>
        public bool DiscardSlot(int slotIndex)
        {
            var data = _repository.Data?.Inventory;
            if (data == null || slotIndex < 0 || slotIndex >= data.Slots.Count)
                return false;

            data.Slots.RemoveAt(slotIndex);

            _repository.MarkDirty();
            return true;
        }
    }
}
