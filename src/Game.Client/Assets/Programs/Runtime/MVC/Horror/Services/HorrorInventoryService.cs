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
    /// スロット位置は各行の SlotNo（0〜MaxSlotCount-1、行間で一意）が持ち、行の存在 = 中身のあるスタック。
    /// 行削除で他行の位置は動かない（前詰めしない）。
    /// 同一 (ObjectCategory, Id) はスタック上限（maxCount）を超えると複数スロットに分割して保持する。
    /// </summary>
    public class HorrorInventoryService : IHorrorInventoryService
    {
        private const int MaxSlotCount = HorrorInventoryConstants.MaxSlotCount;

        /// <summary>所持アイテム一覧（読み取り専用、疎。位置は各行の SlotNo が持ち、並び順に意味はない）。未ロード時は空。</summary>
        public IReadOnlyList<HorrorInventorySlotData> Slots => _repository.Data?.Inventory?.Slots ?? _emptySlots;

        private readonly IReadOnlyList<HorrorInventorySlotData> _emptySlots = Array.Empty<HorrorInventorySlotData>();

        private readonly IHorrorSaveRepository _repository;

        public HorrorInventoryService(IHorrorSaveRepository repository)
        {
            _repository = repository;
        }

        /// <summary>
        /// アイテムをインベントリに追加する。既存スタックを SlotNo 昇順に maxCount まで充填し、
        /// 超過分は最小の空き位置から新規スタックとして分割配置する。
        /// 全量が入らない場合（空き位置も尽きる場合）は何もせず false（全量成功 or 完全失敗）。
        /// </summary>
        public bool TryAdd(ObjectCategory category, int id, int addCount, int maxCount)
        {
            var data = _repository.Data?.Inventory;
            if (data == null || addCount <= 0 || maxCount <= 0)
                return false;

            var map = BuildSlotMap(data);

            // 全量が入るか事前判定する（既存スタックの空き + 空き位置数 × maxCount）
            long capacity = 0;
            foreach (var slot in map)
            {
                if (slot == null)
                    capacity += maxCount;
                else if (slot.ObjectCategory == category && slot.Id == id)
                    capacity += Mathf.Max(0, maxCount - slot.Count);
            }

            if (capacity < addCount)
                return false;

            // 既存スタックを SlotNo 昇順で充填する
            int remaining = addCount;
            for (int pos = 0; pos < MaxSlotCount && remaining > 0; pos++)
            {
                var slot = map[pos];
                if (slot == null || slot.ObjectCategory != category || slot.Id != id)
                    continue;

                int fill = Mathf.Min(maxCount - slot.Count, remaining);
                if (fill <= 0)
                    continue;

                slot.Count += fill;
                remaining -= fill;
            }

            // 残量は最小の空き位置から新規スタックとして配置する
            for (int pos = 0; pos < MaxSlotCount && remaining > 0; pos++)
            {
                if (map[pos] != null)
                    continue;

                int fill = Mathf.Min(maxCount, remaining);
                var newSlot = new HorrorInventorySlotData
                {
                    ObjectCategory = category,
                    Id = id,
                    Count = fill,
                    SlotNo = pos
                };
                data.Slots.Add(newSlot);
                map[pos] = newSlot;
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
        /// 指定数を消費する。複数スロットにまたがる場合は SlotNo 昇順（画面の若い位置）から消費し、
        /// 0 になった行は除去する（他行の位置は動かない）。合計所持数が不足なら何もせず false（部分消費しない）。
        /// </summary>
        public bool TryConsume(ObjectCategory category, int id, int count)
        {
            var data = _repository.Data?.Inventory;
            if (data == null || count <= 0)
                return false;

            if (GetCount(category, id) < count)
                return false;

            var map = BuildSlotMap(data);
            int remaining = count;
            for (int pos = 0; pos < MaxSlotCount && remaining > 0; pos++)
            {
                var slot = map[pos];
                if (slot == null || slot.ObjectCategory != category || slot.Id != id)
                    continue;

                int take = Mathf.Min(slot.Count, remaining);
                slot.Count -= take;
                remaining -= take;
            }

            data.Slots.RemoveAll(s => s.ObjectCategory == category && s.Id == id && s.Count <= 0);

            _repository.MarkDirty();
            return true;
        }

        /// <summary>指定位置（SlotNo）のスロットを丸ごと破棄する。範囲外・空位置は何もせず false。</summary>
        public bool DiscardSlot(int slotIndex)
        {
            var data = _repository.Data?.Inventory;
            if (data == null || slotIndex < 0 || slotIndex >= MaxSlotCount)
                return false;

            int index = data.Slots.FindIndex(s => s.SlotNo == slotIndex);
            if (index < 0)
                return false;

            data.Slots.RemoveAt(index);

            _repository.MarkDirty();
            return true;
        }

        /// <summary>
        /// 位置→行の参照配列を構築する。SlotNo 昇順走査と空き位置探索の基盤。
        /// 範囲外・重複 SlotNo（正規化後は存在しない不変条件違反）は LogError の上でスキップする。
        /// </summary>
        private static HorrorInventorySlotData[] BuildSlotMap(HorrorInventorySaveData data)
        {
            var map = new HorrorInventorySlotData[MaxSlotCount];
            foreach (var slot in data.Slots)
            {
                if (slot.SlotNo < 0 || slot.SlotNo >= MaxSlotCount || map[slot.SlotNo] != null)
                {
                    Debug.LogError(
                        $"[{nameof(HorrorInventoryService)}] SlotNo の不変条件違反を検出しました: " +
                        $"SlotNo={slot.SlotNo}, ({slot.ObjectCategory}, {slot.Id}) x{slot.Count}");
                    continue;
                }

                map[slot.SlotNo] = slot;
            }

            return map;
        }
    }
}
