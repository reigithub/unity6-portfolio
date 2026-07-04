using System.Collections.Generic;
using Game.Core.Services;
using Game.Horror.SaveData;
using Game.Horror.Services.Interfaces;
using Game.Shared.Enums;
using Game.Shared.SaveData;
using Game.Shared.Services;
using UnityEngine;

namespace Game.Horror.Services
{
    /// <summary>
    /// Horror 装備ショートカット（D-Pad 4スロット）のセーブサービス。
    /// 登録の保持・整合・永続化を担う。<see cref="HorrorInventorySaveService"/> と同型。
    /// </summary>
    public class HorrorEquipmentShortcutSaveService : SaveServiceBase<HorrorEquipmentShortcutSaveData>, IHorrorEquipmentShortcutSaveService, IGameService
    {
        protected override string SaveKey => "horror_equipment_shortcut";
        protected override int CurrentVersion => 1;

        /// <summary>ショートカットスロット数（D-Pad 1〜4）。</summary>
        public const int SlotCount = 4;

        private readonly IScriptableDatabaseService _databaseService;

        public HorrorEquipmentShortcutSaveService(ISaveDataStorage storage, IScriptableDatabaseService databaseService) : base(storage)
        {
            _databaseService = databaseService;
        }

        /// <summary>指定スロット(0-3)へアイテム (SlotType, Id) を登録する。</summary>
        public bool Set(int index, InventorySlotType slotType, int id)
        {
            if (Data == null || index < 0 || index >= SlotCount)
                return false;

            var slot = Data.Slots[index];
            slot.SlotType = slotType;
            slot.Id = id;
            MarkDirty();
            return true;
        }

        /// <summary>指定スロット(0-3)の登録を外す（空にする）。</summary>
        public bool Clear(int index)
        {
            if (Data == null || index < 0 || index >= SlotCount)
                return false;

            var slot = Data.Slots[index];
            slot.SlotType = InventorySlotType.None;
            slot.Id = 0;
            MarkDirty();
            return true;
        }

        /// <summary>指定スロットの登録を取得する。空(None)または範囲外なら false。</summary>
        public bool TryGet(int index, out HorrorEquipmentShortcutSlotData slot)
        {
            slot = null;
            if (Data == null || index < 0 || index >= SlotCount)
                return false;

            var s = Data.Slots[index];
            if (s.SlotType == InventorySlotType.None)
                return false;

            slot = s;
            return true;
        }

        protected override HorrorEquipmentShortcutSaveData CreateNewData()
        {
            var data = new HorrorEquipmentShortcutSaveData();
            EnsureSlotCount(data);
            return data;
        }

        protected override void OnDataLoaded(HorrorEquipmentShortcutSaveData data)
        {
            EnsureSlotCount(data);

            var database = _databaseService.Database;
            foreach (var slot in data.Slots)
            {
                if (slot.SlotType == InventorySlotType.None)
                    continue;

                bool exists = slot.SlotType switch
                {
                    InventorySlotType.Item => database.HorrorItemMasterTable.TryFindById(slot.Id, out _),
                    InventorySlotType.Weapon => database.HorrorWeaponMasterTable.TryFindById(slot.Id, out _),
                    _ => false,
                };

                if (!exists)
                {
                    slot.SlotType = InventorySlotType.None;
                    slot.Id = 0;
                }
            }
        }

        // スロット数を SlotCount(4) に揃える（不足は空追加、超過は切り詰め）。
        private static void EnsureSlotCount(HorrorEquipmentShortcutSaveData data)
        {
            data.Slots ??= new List<HorrorEquipmentShortcutSlotData>();
            while (data.Slots.Count < SlotCount)
                data.Slots.Add(new HorrorEquipmentShortcutSlotData());
            if (data.Slots.Count > SlotCount)
                data.Slots.RemoveRange(SlotCount, data.Slots.Count - SlotCount);
        }

        protected override int GetDataVersion(HorrorEquipmentShortcutSaveData data) => data.Version;

        protected override void MigrateData(HorrorEquipmentShortcutSaveData data, int fromVersion)
        {
            data.Version = CurrentVersion;
            Debug.Log($"[HorrorEquipmentShortcutSaveService] Migrated from version {fromVersion} to {CurrentVersion}");
        }
    }
}
