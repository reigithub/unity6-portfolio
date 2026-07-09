using System;
using System.Collections.Generic;
using Game.Horror.Constants;
using Game.Horror.SaveData;
using Game.Horror.Services.Interfaces;
using Game.Shared.Enums;
using Game.Shared.Interfaces;
using UnityEngine;

namespace Game.Horror.Services
{
    /// <summary>
    /// Horror インベントリ（所持アイテム）を扱うドメインサービス。
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
        /// アイテムをインベントリに追加する。
        /// 同一 Id が既に存在する場合はスタック加算し MaxCount で頭打ちする。
        /// </summary>
        public bool TryAdd(IHorrorInventorySlotInfo info, int addCount)
        {
            var data = _repository.Data?.Inventory;
            if (data == null || info == null || addCount <= 0)
                return false;

            if (TryGet(data, info.SlotType, info.Id, out var slot))
            {
                if (slot.Count >= info.MaxCount)
                    return false;

                slot.Count = Mathf.Min(slot.Count + addCount, info.MaxCount);
            }
            else
            {
                if (data.Slots.Count >= MaxSlotCount)
                    return false;

                data.Slots.Add(new HorrorInventorySlotData
                {
                    SlotType = info.SlotType,
                    Id = info.Id,
                    Count = Mathf.Min(addCount, info.MaxCount)
                });
            }

            _repository.MarkDirty();
            return true;
        }

        private static bool TryGet(HorrorInventorySaveData data, InventorySlotType type, int id, out HorrorInventorySlotData slot)
        {
            foreach (var slotData in data.Slots)
            {
                if (slotData.SlotType == type && slotData.Id == id)
                {
                    slot = slotData;
                    return true;
                }
            }

            slot = null;
            return false;
        }

        /// <summary>指定 (SlotType, Id) を所持しているか判定する。</summary>
        public bool HasItem(InventorySlotType type, int id)
        {
            var data = _repository.Data?.Inventory;
            return data != null && TryGet(data, type, id, out _);
        }

        /// <summary>指定 (SlotType, Id) の所持数を取得する。未所持は 0。</summary>
        public int GetCount(InventorySlotType type, int id)
        {
            var data = _repository.Data?.Inventory;
            return data != null && TryGet(data, type, id, out var slot) ? slot.Count : 0;
        }

        /// <summary>
        /// 指定数を消費する。所持数不足なら何もせず false（部分消費しない）。
        /// 0 到達でスロットを除去し、Dirty にする。
        /// </summary>
        public bool TryConsume(InventorySlotType type, int id, int count)
        {
            var data = _repository.Data?.Inventory;
            if (data == null || count <= 0)
                return false;

            if (!TryGet(data, type, id, out var slot) || slot.Count < count)
                return false;

            slot.Count -= count;
            if (slot.Count <= 0)
                data.Slots.Remove(slot);

            _repository.MarkDirty();
            return true;
        }
    }
}
