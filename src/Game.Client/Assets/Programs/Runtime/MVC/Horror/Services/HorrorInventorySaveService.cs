using Game.Core.Services;
using Game.Horror.SaveData;
using Game.Horror.Services.Interfaces;
using Game.Shared.Enums;
using Game.Shared.Interfaces;
using Game.Shared.SaveData;
using Game.Shared.Services;
using UnityEngine;

namespace Game.Horror.Services
{
    /// <summary>
    /// Horror インベントリのセーブサービス。所持アイテムの状態保持と永続化を担う。
    /// </summary>
    public class HorrorInventorySaveService : SaveServiceBase<HorrorInventorySaveData>, IHorrorInventorySaveService, IGameService
    {
        protected override string SaveKey => "horror_inventory";
        protected override int CurrentVersion => 1;

        private readonly IScriptableDatabaseService _databaseService;

        private const int MaxSlotCount = 30;

        public HorrorInventorySaveService(ISaveDataStorage storage, IScriptableDatabaseService databaseService) : base(storage)
        {
            _databaseService = databaseService;
        }

        /// <summary>
        /// アイテムをインベントリに追加する。
        /// 同一 Id が既に存在する場合はスタック加算し MaxCount で頭打ちする。
        /// </summary>
        public bool TryAdd(IHorrorInventorySlotInfo info, int addCount)
        {
            if (info == null || addCount <= 0)
                return false;

            if (TryGet(info.SlotType, info.Id, out var slot))
            {
                if (slot.Count >= info.MaxCount)
                    return false;

                slot.Count = Mathf.Min(slot.Count + addCount, info.MaxCount);
            }
            else
            {
                if (Data.Slots.Count >= MaxSlotCount)
                    return false;

                Data.Slots.Add(new HorrorInventorySlotData
                {
                    SlotType = info.SlotType,
                    Id = info.Id,
                    Count = Mathf.Min(addCount, info.MaxCount)
                });
            }

            MarkDirty();
            return true;
        }

        private bool TryGet(InventorySlotType type, int id, out HorrorInventorySlotData slot)
        {
            foreach (var slotData in Data.Slots)
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

        public bool HasItem(int itemId) => TryGet(InventorySlotType.Item, itemId, out _);

        protected override void OnDataLoaded(HorrorInventorySaveData data)
        {
            var database = _databaseService.Database;
            // 逆順走査
            for (int i = data.Slots.Count - 1; i >= 0; i--)
            {
                var slot = data.Slots[i];
                switch (slot.SlotType)
                {
                    case InventorySlotType.Item:
                    {
                        if (!database.HorrorItemMasterTable.TryFindById(slot.Id, out var master))
                            data.Slots.RemoveAt(i);
                        else
                            data.Slots[i].Count = Mathf.Min(slot.Count, master.MaxCount);
                        break;
                    }
                    case InventorySlotType.Weapon:
                    {
                        if (!database.HorrorWeaponMasterTable.TryFindById(slot.Id, out var master))
                            data.Slots.RemoveAt(i);
                        else
                            data.Slots[i].Count = Mathf.Min(slot.Count, master.MaxCount);
                        break;
                    }
                }
            }
        }

        protected override int GetDataVersion(HorrorInventorySaveData data) => data.Version;

        protected override void MigrateData(HorrorInventorySaveData data, int fromVersion)
        {
            data.Version = CurrentVersion;
            Debug.Log($"[HorrorInventorySaveService] Migrated from version {fromVersion} to {CurrentVersion}");
        }
    }
}
