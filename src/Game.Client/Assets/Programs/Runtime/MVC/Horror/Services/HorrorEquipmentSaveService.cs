using Game.Core.Services;
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
    /// Horror 装備状態のセーブサービス。装備中武器の保持と永続化を担う。
    /// </summary>
    public class HorrorEquipmentSaveService : SaveServiceBase<HorrorEquipmentSaveData>, IHorrorEquipmentSaveService, IGameService
    {
        protected override string SaveKey => "horror_equipment";
        protected override int CurrentVersion => 1;

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

        protected override void OnDataLoaded(HorrorEquipmentSaveData data)
        {
            if (data.SlotType != InventorySlotType.Weapon)
            {
                data.SlotType = InventorySlotType.None;
                data.Id = 0;
                return;
            }

            var database = _databaseService.Database;
            if (!HorrorInventoryHelper.TryGetSlotInfo(database, data.SlotType, data.Id, out _))
            {
                data.SlotType = InventorySlotType.None;
                data.Id = 0;
            }
        }

        protected override int GetDataVersion(HorrorEquipmentSaveData data) => data.Version;

        protected override void MigrateData(HorrorEquipmentSaveData data, int fromVersion)
        {
            data.Version = CurrentVersion;
            Debug.Log($"[HorrorEquipmentSaveService] Migrated from version {fromVersion} to {CurrentVersion}");
        }
    }
}
