using System.Collections.Generic;
using Game.Core.Services;
using Game.Horror.Constants;
using Game.Horror.Inventory;
using Game.Horror.SaveData;
using Game.Horror.Services.Interfaces;
using Game.Shared.Enums;
using Game.Shared.SaveData;
using Game.Shared.Scriptable.Database;
using Game.Shared.Services;
using UnityEngine;

namespace Game.Horror.Services
{
    /// <summary>
    /// Horror セーブデータ（<see cref="HorrorSaveData"/>）の永続化リポジトリ。
    /// 読み書きと区画ごとのマスター整合（正規化）を担い、ビジネスロジックは各ドメインサービスへ委譲する。
    /// </summary>
    public class HorrorSaveRepository : SaveRepositoryBase<HorrorSaveData>, IHorrorSaveRepository, IGameService
    {
        protected override string SaveKey => "horror_save";
        protected override int CurrentVersion => 1;

        /// <summary>ショートカットスロット数（D-Pad 1〜4）。</summary>
        private const int MaxSlotCount = HorrorEquipmentConstants.MaxSlotCount;

        private readonly IScriptableDatabaseService _databaseService;

        public HorrorSaveRepository(ISaveDataStorage storage, IScriptableDatabaseService databaseService) : base(storage)
        {
            _databaseService = databaseService;
        }

        protected override HorrorSaveData CreateNewData()
        {
            var data = new HorrorSaveData();
            EnsureSlotCount(data.Equipment);
            return data;
        }

        protected override void OnDataLoaded(HorrorSaveData data)
        {
            data.Player ??= new HorrorPlayerSaveData();
            data.Inventory ??= new HorrorInventorySaveData();
            data.Equipment ??= new HorrorEquipmentSaveData();
            data.Interaction ??= new HorrorInteractionSaveData();

            var database = _databaseService.Database;

            NormalizeInteraction(data.Interaction, database);
            NormalizeInventory(data.Inventory, database);
            NormalizeEquipment(data.Equipment, database);
            NormalizePlayer(data.Player, database);
        }

        private static void NormalizeInteraction(HorrorInteractionSaveData data, ScriptableDatabase database)
        {
            // 逆順走査
            for (int i = data.InteractionIds.Count - 1; i >= 0; i--)
            {
                var id = data.InteractionIds[i];
                if (!database.HorrorInteractionMasterTable.TryFindById(id, out _))
                    data.InteractionIds.RemoveAt(i);
            }
        }

        private static void NormalizeInventory(HorrorInventorySaveData data, ScriptableDatabase database)
        {
            // 逆順走査
            for (int i = data.Slots.Count - 1; i >= 0; i--)
            {
                var slot = data.Slots[i];
                if (!HorrorInventoryHelper.TryGetSlotInfo(database, slot.SlotType, slot.Id, out var info))
                    data.Slots.RemoveAt(i);
                else
                    data.Slots[i].Count = Mathf.Min(slot.Count, info.MaxCount);
            }
        }

        private static void NormalizeEquipment(HorrorEquipmentSaveData data, ScriptableDatabase database)
        {
            EnsureSlotCount(data);

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

            data.Magazines ??= new List<HorrorWeaponMagazineData>();

            // 逆順走査（弾薬概念の無い武器・重複・マスター未存在レコードを除去しつつクランプ）
            var seenWeaponIds = new HashSet<int>();
            for (int i = data.Magazines.Count - 1; i >= 0; i--)
            {
                var rec = data.Magazines[i];
                if (!database.HorrorWeaponMasterTable.TryFindById(rec.WeaponId, out var weaponMaster)
                    || weaponMaster.AmmoItemId <= 0
                    || !seenWeaponIds.Add(rec.WeaponId))
                {
                    data.Magazines.RemoveAt(i);
                }
                else
                {
                    rec.Count = Mathf.Clamp(rec.Count, 0, weaponMaster.MagazineSize);
                }
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

        private static void NormalizePlayer(HorrorPlayerSaveData data, ScriptableDatabase database)
        {
            // マスター不在 Id は未記録(0)へ戻す。シーン内に該当セーブポイントが無いケースは復元側のフォールバックが担う
            if (data.LastSavepointId != 0 && !database.HorrorInteractionMasterTable.TryFindById(data.LastSavepointId, out _))
            {
                data.LastSavepointId = 0;
            }
        }

        protected override int GetDataVersion(HorrorSaveData data) => data.Version;

        protected override void MigrateData(HorrorSaveData data, int fromVersion)
        {
            data.Version = CurrentVersion;
            Debug.Log($"[HorrorSaveRepository] Migrated from version {fromVersion} to {CurrentVersion}");
        }
    }
}
