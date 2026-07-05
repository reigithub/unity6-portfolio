using System.Collections.Generic;
using Game.Core.Services;
using Game.Horror.Constants;
using Game.Horror.Inventory;
using Game.Horror.SaveData;
using Game.Horror.Services.Interfaces;
using Game.Shared.Enums;
using Game.Shared.SaveData;
using Game.Shared.Services;
using UnityEngine;

namespace Game.Horror.Services
{
    /// <summary>
    /// Horror 装備状態のセーブサービス。装備中武器の保持と、装備ショートカット(D-Pad 4スロット)の登録・整合を合わせて担う。
    /// </summary>
    public class HorrorEquipmentSaveService : SaveServiceBase<HorrorEquipmentSaveData>, IHorrorEquipmentSaveService, IGameService
    {
        protected override string SaveKey => "horror_equipment";
        protected override int CurrentVersion => 1;

        /// <summary>ショートカットスロット数（D-Pad 1〜4）。</summary>
        private const int MaxSlotCount = HorrorEquipmentConstants.MaxSlotCount;

        private readonly IScriptableDatabaseService _databaseService;
        private readonly IHorrorInventorySaveService _inventoryService;

        public HorrorEquipmentSaveService(ISaveDataStorage storage, IScriptableDatabaseService databaseService, IHorrorInventorySaveService inventoryService) : base(storage)
        {
            _databaseService = databaseService;
            _inventoryService = inventoryService;
        }

        /// <summary>
        /// 指定 (SlotType, Id) が装備可能か判定する。装備対象は Weapon のみで、かつ所持している必要がある。
        /// </summary>
        public bool CanEquip(InventorySlotType type, int id) => type == InventorySlotType.Weapon && _inventoryService.HasItem(type, id);

        /// <summary>
        /// 指定 (SlotType, Id) を装備状態にする。<see cref="CanEquip"/> が成立する場合のみ反映して Dirty にする。
        /// 現在と同一の装備を指定した場合も冪等に true を返す。
        /// </summary>
        public bool TryEquip(InventorySlotType type, int id)
        {
            if (Data == null || !CanEquip(type, id))
                return false;

            Data.SlotType = type;
            Data.Id = id;
            MarkDirty();
            return true;
        }

        /// <summary>現在装備中の (SlotType, Id) を取得する。未装備または未ロードなら false。</summary>
        public bool TryGetEquipped(out InventorySlotType type, out int id)
        {
            type = InventorySlotType.None;
            id = 0;

            if (Data == null || Data.SlotType == InventorySlotType.None)
                return false;

            type = Data.SlotType;
            id = Data.Id;
            return true;
        }

        /// <summary>指定スロット(0-3)へアイテム (SlotType, Id) を登録する。</summary>
        public bool TrySetSlot(int index, InventorySlotType slotType, int id)
        {
            if (Data == null || index < 0 || index >= MaxSlotCount)
                return false;

            var slot = Data.Slots[index];
            slot.SlotType = slotType;
            slot.Id = id;
            MarkDirty();
            return true;
        }

        /// <summary>
        /// 対象アイテムを destIndex に割り当てる。同一アイテムが既に別スロットにあれば旧スロットと内容を交換
        /// （交換先が空なら実質「移動」）、無ければ上書き。単一登録（同一アイテムは高々1スロット）を保つ。
        /// </summary>
        public bool AssignSlot(int destIndex, InventorySlotType slotType, int id)
        {
            if (Data == null || destIndex < 0 || destIndex >= MaxSlotCount)
                return false;

            int index = GetSlotIndex(slotType, id);
            if (index == destIndex)
                return false; // 既に同じスロット → 変化なし

            var dest = Data.Slots[destIndex];
            if (index >= 0)
            {
                // 既登録 → 旧スロットへ dest の旧内容を移す（dest が空なら旧が空になり「移動」、占有なら入替）
                var src = Data.Slots[index];
                src.SlotType = dest.SlotType;
                src.Id = dest.Id;
            }

            // dest に対象を置く（未登録時は上書き）
            dest.SlotType = slotType;
            dest.Id = id;
            MarkDirty();
            return true;
        }

        // 指定アイテム (SlotType, Id) が登録されているスロット index を返す（None は対象外）。無ければ -1。
        private int GetSlotIndex(InventorySlotType slotType, int id)
        {
            if (Data == null || slotType == InventorySlotType.None)
                return -1;

            for (int i = 0; i < MaxSlotCount; i++)
            {
                var s = Data.Slots[i];
                if (s.SlotType == slotType && s.Id == id)
                    return i;
            }
            return -1;
        }

        /// <summary>指定スロット(0-3)の登録を外す（空にする）。</summary>
        public bool ClearSlot(int index)
        {
            if (Data == null || index < 0 || index >= MaxSlotCount)
                return false;

            var slot = Data.Slots[index];
            slot.SlotType = InventorySlotType.None;
            slot.Id = 0;
            MarkDirty();
            return true;
        }

        /// <summary>指定スロットの登録を取得する。空(None)または範囲外なら false。</summary>
        public bool TryGetSlot(int index, out HorrorEquipmentSlotData slot)
        {
            slot = null;
            if (Data == null || index < 0 || index >= MaxSlotCount)
                return false;

            var s = Data.Slots[index];
            if (s.SlotType == InventorySlotType.None)
                return false;

            slot = s;
            return true;
        }

        protected override HorrorEquipmentSaveData CreateNewData()
        {
            var data = new HorrorEquipmentSaveData();
            EnsureSlotCount(data);
            return data;
        }

        protected override void OnDataLoaded(HorrorEquipmentSaveData data)
        {
            EnsureSlotCount(data);

            var database = _databaseService.Database;

            foreach (var slot in data.Slots)
            {
                if (!HorrorInventoryHelper.TryGetSlotInfo(database, slot.SlotType, slot.Id, out _))
                {
                    slot.SlotType = InventorySlotType.None;
                    slot.Id = 0;
                }
            }

            if (data.SlotType != InventorySlotType.Weapon || !HorrorInventoryHelper.TryGetSlotInfo(database, data.SlotType, data.Id, out _))
            {
                data.SlotType = InventorySlotType.None;
                data.Id = 0;
            }
        }

        // スロット数を SlotCount(4) に揃える（不足は空追加、超過は切り詰め）。
        private static void EnsureSlotCount(HorrorEquipmentSaveData data)
        {
            data.Slots ??= new List<HorrorEquipmentSlotData>();

            while (data.Slots.Count < MaxSlotCount)
                data.Slots.Add(new HorrorEquipmentSlotData());

            if (data.Slots.Count > MaxSlotCount)
                data.Slots.RemoveRange(MaxSlotCount, data.Slots.Count - MaxSlotCount);
        }

        protected override int GetDataVersion(HorrorEquipmentSaveData data) => data.Version;

        protected override void MigrateData(HorrorEquipmentSaveData data, int fromVersion)
        {
            data.Version = CurrentVersion;
            Debug.Log($"[HorrorEquipmentSaveService] Migrated from version {fromVersion} to {CurrentVersion}");
        }
    }
}
