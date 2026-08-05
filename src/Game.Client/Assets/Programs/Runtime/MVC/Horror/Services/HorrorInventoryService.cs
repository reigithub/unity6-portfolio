using System;
using System.Collections.Generic;
using System.Linq;
using Game.Horror.Constants;
using Game.Horror.Inventory;
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
            if (CalculateCapacity(map, BuildSlotCounts(map), category, id, maxCount) < addCount)
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
        /// 指定数を消費する（スロットを対象に取らない、総量に対する消費。リロード等）。
        /// 複数スロットにまたがる場合は Count 昇順（同数は SlotNo 昇順）で数の少ない端数の山から消費し、
        /// 0 になった行は除去する（他行の位置は動かない）。合計所持数が不足なら何もせず false（部分消費しない）。
        /// </summary>
        public bool TryConsume(ObjectCategory category, int id, int count)
        {
            var data = _repository.Data?.Inventory;
            if (data == null || count <= 0)
                return false;

            if (GetCount(category, id) < count)
                return false;

            // 端数の山を先に解消するため、数の少ない順（同数は画面の若い位置順）に消費する
            var stacks = data.Slots
                .Where(s => s.ObjectCategory == category && s.Id == id)
                .OrderBy(s => s.Count)
                .ThenBy(s => s.SlotNo);

            int remaining = count;
            foreach (var slot in stacks)
            {
                if (remaining <= 0)
                    break;

                int take = Mathf.Min(slot.Count, remaining);
                slot.Count -= take;
                remaining -= take;
            }

            data.Slots.RemoveAll(s => s.ObjectCategory == category && s.Id == id && s.Count <= 0);

            _repository.MarkDirty();
            return true;
        }

        /// <summary>
        /// 指定位置（SlotNo）のスロットのみから指定数を消費する（UI でスロットを選択して実行するアクション用）。
        /// 指定位置の行が (category, id) と一致し Count が足りる場合のみ消費し、0 になった行は除去する（他行の位置は動かない）。
        /// 範囲外・空位置・内容不一致・数量不足は何もせず false（他の同種スロットへは波及しない）。
        /// </summary>
        public bool TryConsumeAt(ObjectCategory category, int id, int slotNo, int count)
        {
            var data = _repository.Data?.Inventory;
            if (data == null || count <= 0 || slotNo < 0 || slotNo >= MaxSlotCount)
                return false;

            int index = data.Slots.FindIndex(s => s.SlotNo == slotNo);
            if (index < 0)
                return false;

            var slot = data.Slots[index];
            if (slot.ObjectCategory != category || slot.Id != id || slot.Count < count)
                return false;

            slot.Count -= count;
            if (slot.Count <= 0)
                data.Slots.RemoveAt(index);

            _repository.MarkDirty();
            return true;
        }

        /// <summary>
        /// 指定の消費を適用した後の状態で、対象を全量追加できるかを判定する（インベントリは変更しない）。
        /// 消費と追加を 1 操作として扱う交換（クラフト等）で、素材だけ消えて成果物が入らない事態を防ぐために使う。
        /// 消費側の所持数が足りない場合も false。
        /// </summary>
        public bool CanAddAfterConsume(IReadOnlyList<HorrorObjectAmount> consumptions, ObjectCategory addCategory, int addId, int addCount, int addMaxCount)
        {
            var data = _repository.Data?.Inventory;
            if (data == null || addCount <= 0 || addMaxCount <= 0)
                return false;

            var map = BuildSlotMap(data);
            var counts = BuildSlotCounts(map);

            if (consumptions != null)
            {
                foreach (var consumption in consumptions)
                {
                    if (!TryConsumeOnCounts(map, counts, consumption))
                        return false;
                }
            }

            return CalculateCapacity(map, counts, addCategory, addId, addMaxCount) >= addCount;
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

        /// <summary>位置ごとの所持数を写した作業用配列を作る。判定のシミュレーションはこの配列だけを書き換える。</summary>
        private static int[] BuildSlotCounts(HorrorInventorySlotData[] map)
        {
            var counts = new int[map.Length];
            for (int pos = 0; pos < map.Length; pos++)
            {
                counts[pos] = map[pos]?.Count ?? 0;
            }

            return counts;
        }

        /// <summary>
        /// 追加可能な総量を求める（空き位置数 × maxCount + 同種スタックの残容量）。
        /// 位置の中身は <paramref name="map"/>、残数は <paramref name="counts"/> を見るため、
        /// 消費をシミュレートした後の状態にも使える（残数 0 の位置は空きとして数える）。
        /// </summary>
        private static long CalculateCapacity(HorrorInventorySlotData[] map, int[] counts, ObjectCategory category, int id, int maxCount)
        {
            long capacity = 0;
            for (int pos = 0; pos < map.Length; pos++)
            {
                var slot = map[pos];
                if (slot == null || counts[pos] <= 0)
                    capacity += maxCount;
                else if (slot.ObjectCategory == category && slot.Id == id)
                    capacity += Mathf.Max(0, maxCount - counts[pos]);
            }

            return capacity;
        }

        /// <summary>
        /// 作業用配列に対して消費をシミュレートする。<see cref="TryConsume"/> と同じ順序
        /// （残数の少ない山から、同数は SlotNo 昇順）で減らす。消費順序によって空く位置の数が変わるため、
        /// 実際の消費と同じ順序で辿らないと追加可能量の判定がずれる。所持数が不足していれば false。
        /// </summary>
        private static bool TryConsumeOnCounts(HorrorInventorySlotData[] map, int[] counts, HorrorObjectAmount consumption)
        {
            if (consumption.Count <= 0)
                return false;

            int remaining = consumption.Count;
            while (remaining > 0)
            {
                int target = -1;
                for (int pos = 0; pos < map.Length; pos++)
                {
                    var slot = map[pos];
                    if (slot == null || counts[pos] <= 0)
                        continue;
                    if (slot.ObjectCategory != consumption.Category || slot.Id != consumption.Id)
                        continue;

                    // 同数なら先に見つかった位置（SlotNo の小さい方）を維持する
                    if (target < 0 || counts[pos] < counts[target])
                        target = pos;
                }

                if (target < 0)
                    return false;

                int take = Mathf.Min(counts[target], remaining);
                counts[target] -= take;
                remaining -= take;
            }

            return true;
        }
    }
}
