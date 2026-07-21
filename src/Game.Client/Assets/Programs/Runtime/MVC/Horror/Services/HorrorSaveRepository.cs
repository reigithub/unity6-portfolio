using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.Constants;
using Game.Horror.Inventory;
using Game.Horror.SaveData;
using Game.Horror.Services.Interfaces;
using Game.Shared.Enums;
using Game.Shared.SaveData;
using Game.Shared.Scriptable.Database;
using Game.Shared.Services;
using Game.Shared.Services.Interfaces;
using UnityEngine;

namespace Game.Horror.Services
{
    /// <summary>
    /// Horror セーブデータ（<see cref="HorrorSaveData"/>）の永続化リポジトリ。
    /// 読み書きと区画ごとのマスター整合（正規化）を担い、ビジネスロジックは各ドメインサービスへ委譲する。
    /// </summary>
    public class HorrorSaveRepository : SaveRepositoryBase<HorrorSaveData>, IHorrorSaveRepository, IGameService
    {
        protected override string SaveKey => GetSaveKeyBySlot(CurrentSlot);
        protected override int CurrentVersion => HorrorSaveConstants.SaveDataLatestVersion;

        public int CurrentSlot { get; private set; } = -1;

        private const int MaxSaveSlotCount = HorrorSaveConstants.MaxSaveSlotCount;
        private const int MaxEquipmentSlotCount = HorrorEquipmentConstants.MaxEquipmentSlotCount;

        private readonly IScriptableDatabaseService _databaseService;

        public HorrorSaveRepository(ISaveDataStorage storage, IScriptableDatabaseService databaseService) : base(storage)
        {
            _databaseService = databaseService;
        }

        private static string GetSaveKeyBySlot(int slotNo) => $"horror_save_slot{slotNo}";

        public async UniTask<HorrorSaveSlotInfo> LoadSlotInfoAsync(int slotNo)
        {
            var data = await _storage.LoadAsync<HorrorSaveData>(GetSaveKeyBySlot(slotNo));

            return new HorrorSaveSlotInfo
            {
                SlotNo = slotNo,
                HasData = data != null,
                SavedAtUtc = data?.SavedAtUtc ?? default,
                SavepointId = data?.SavepointId ?? 0
            };
        }

        /// <summary>
        /// 全スロットのメタ情報を並列に取得する。現在ロード中の <see cref="ISaveRepository{TData}.Data"/> は変更しない。
        /// </summary>
        public async UniTask<HorrorSaveSlotInfo[]> LoadSlotInfosAsync()
        {
            var tasks = new UniTask<HorrorSaveSlotInfo>[MaxSaveSlotCount];
            for (int slot = 0; slot < MaxSaveSlotCount; slot++)
            {
                tasks[slot] = LoadSlotInfoAsync(slot);
            }

            return await UniTask.WhenAll(tasks);
        }

        public async UniTask LoadByCurrentSlotAsync()
        {
            if (!IsValidSlot(CurrentSlot)) return;

            await LoadAsync();
        }

        public async UniTask LoadBySlotAsync(int slotNo)
        {
            if (!IsValidSlot(slotNo)) return;

            CurrentSlot = slotNo;
            await LoadAsync();
        }

        /// <summary>
        /// 指定スロットへ保存する。範囲外のスロット番号は保存を行わない。
        /// スロットメタ（スロット番号・保存日時・セーブポイント Id）は保存直前に <see cref="OnBeforeSave"/> が刻印する。
        /// </summary>
        /// <param name="slotNo">保存先スロット番号（0〜<see cref="HorrorSaveConstants.MaxSaveSlotCount"/> - 1）。</param>
        public async UniTask SaveBySlotAsync(int slotNo)
        {
            if (!IsValidSlot(slotNo)) return;

            CurrentSlot = slotNo;
            await SaveAsync();
        }

        public async UniTask DeleteBySlotAsync(int slotNo)
        {
            if (!IsValidSlot(slotNo)) return;

            int slot = CurrentSlot;
            try
            {
                CurrentSlot = slotNo;
                await DeleteAsync();
            }
            finally
            {
                CurrentSlot = slot;
            }
        }

        private bool IsValidSlot(int slotNo)
        {
            if (slotNo < 0 || slotNo >= MaxSaveSlotCount)
            {
                Debug.LogError($"[{GetType().Name}] Invalid slot number: {slotNo}");
                return false;
            }

            return true;
        }

        // どの保存経路（SaveAsync / SaveIfDirtyAsync）でもスロットメタが最新になるよう、保存直前に刻印する。
        // セーブポイント Id は復帰地点（Player.LastSavepointId）から導出し、真実の源を一つに保つ。
        protected override void OnBeforeSave(HorrorSaveData data)
        {
            if (CurrentSlot < 0) throw new InvalidOperationException($"[{GetType().Name}] Slot {CurrentSlot} is invalid");

            data.SlotNo = CurrentSlot;
            data.SavedAtUtc = DateTime.UtcNow;
            data.SavepointId = data.Player.LastSavepointId;
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
                    slot.SlotType = ObjectCategory.None;
                    slot.Id = 0;
                }
            }

            if (data.SlotType != ObjectCategory.Weapon || !HorrorInventoryHelper.TryGetSlotInfo(database, data.SlotType, data.Id, out _))
            {
                data.SlotType = ObjectCategory.None;
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

            while (data.Slots.Count < MaxEquipmentSlotCount)
                data.Slots.Add(new HorrorEquipmentSlotData());

            if (data.Slots.Count > MaxEquipmentSlotCount)
                data.Slots.RemoveRange(MaxEquipmentSlotCount, data.Slots.Count - MaxEquipmentSlotCount);
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
