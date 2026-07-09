using System.Collections.Generic;
using Game.Horror.Constants;
using Game.Horror.SaveData;
using Game.Horror.Services.Interfaces;
using Game.Shared.Enums;
using UnityEngine;

namespace Game.Horror.Services
{
    /// <summary>
    /// Horror 装備状態を扱うドメインサービス。装備中武器の保持と、装備ショートカット(D-Pad 4スロット)の登録・整合を合わせて担う。
    /// </summary>
    public class HorrorEquipmentService : IHorrorEquipmentService
    {
        /// <summary>ショートカットスロット数（D-Pad 1〜4）。</summary>
        private const int MaxSlotCount = HorrorEquipmentConstants.MaxSlotCount;

        private readonly IHorrorSaveRepository _repository;
        private readonly IHorrorInventoryService _inventoryService;

        public HorrorEquipmentService(IHorrorSaveRepository repository, IHorrorInventoryService inventoryService)
        {
            _repository = repository;
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
            var data = _repository.Data?.Equipment;
            if (data == null || !CanEquip(type, id))
                return false;

            data.SlotType = type;
            data.Id = id;
            _repository.MarkDirty();
            return true;
        }

        /// <summary>現在装備中の (SlotType, Id) を取得する。未装備または未ロードなら false。</summary>
        public bool TryGetEquipped(out InventorySlotType type, out int id)
        {
            type = InventorySlotType.None;
            id = 0;

            var data = _repository.Data?.Equipment;
            if (data == null || data.SlotType == InventorySlotType.None)
                return false;

            type = data.SlotType;
            id = data.Id;
            return true;
        }

        /// <summary>指定スロット(0-3)へアイテム (SlotType, Id) を登録する。</summary>
        public bool TrySetSlot(int index, InventorySlotType slotType, int id)
        {
            var data = _repository.Data?.Equipment;
            if (data == null || index < 0 || index >= MaxSlotCount)
                return false;

            var slot = data.Slots[index];
            slot.SlotType = slotType;
            slot.Id = id;
            _repository.MarkDirty();
            return true;
        }

        /// <summary>
        /// 対象アイテムを destIndex に割り当てる。同一アイテムが既に別スロットにあれば旧スロットと内容を交換
        /// （交換先が空なら実質「移動」）、無ければ上書き。単一登録（同一アイテムは高々1スロット）を保つ。
        /// </summary>
        public bool TryAssignSlot(int destIndex, InventorySlotType slotType, int id)
        {
            var data = _repository.Data?.Equipment;
            if (data == null || destIndex < 0 || destIndex >= MaxSlotCount)
                return false;

            int index = GetSlotIndex(data, slotType, id);
            if (index == destIndex)
                return false; // 既に同じスロット → 変化なし

            var dest = data.Slots[destIndex];
            if (index >= 0)
            {
                // 既登録 → 旧スロットへ dest の旧内容を移す（dest が空なら旧が空になり「移動」、占有なら入替）
                var src = data.Slots[index];
                src.SlotType = dest.SlotType;
                src.Id = dest.Id;
            }

            // dest に対象を置く（未登録時は上書き）
            dest.SlotType = slotType;
            dest.Id = id;
            _repository.MarkDirty();
            return true;
        }

        // 指定アイテム (SlotType, Id) が登録されているスロット index を返す（None は対象外）。無ければ -1。
        private static int GetSlotIndex(HorrorEquipmentSaveData data, InventorySlotType slotType, int id)
        {
            if (data == null || slotType == InventorySlotType.None)
                return -1;

            for (int i = 0; i < MaxSlotCount; i++)
            {
                var s = data.Slots[i];
                if (s.SlotType == slotType && s.Id == id)
                    return i;
            }
            return -1;
        }

        /// <summary>指定スロット(0-3)の登録を外す（空にする）。</summary>
        public bool ClearSlot(int index)
        {
            var data = _repository.Data?.Equipment;
            if (data == null || index < 0 || index >= MaxSlotCount)
                return false;

            var slot = data.Slots[index];
            slot.SlotType = InventorySlotType.None;
            slot.Id = 0;
            _repository.MarkDirty();
            return true;
        }

        /// <summary>指定スロットの登録を取得する。空(None)または範囲外なら false。</summary>
        public bool TryGetSlot(int index, out HorrorEquipmentSlotData slot)
        {
            slot = null;
            var data = _repository.Data?.Equipment;
            if (data == null || index < 0 || index >= MaxSlotCount)
                return false;

            var s = data.Slots[index];
            if (s.SlotType == InventorySlotType.None)
                return false;

            slot = s;
            return true;
        }

        /// <summary>
        /// 指定武器の弾倉残弾を取得する。記録があれば [0, magazineSize] にクランプして返し、
        /// 未記録・未ロードなら満タン（magazineSize）を返す（初回入手武器は満タン仕様）。読み取り専用で Data には書き込まない。
        /// </summary>
        public int GetMagazineCount(int weaponId, int magazineSize)
        {
            var data = _repository.Data?.Equipment;
            if (data == null || data.Magazines == null)
                return magazineSize;

            foreach (var rec in data.Magazines)
            {
                if (rec.WeaponId == weaponId)
                    return Mathf.Clamp(rec.Count, 0, magazineSize);
            }

            return magazineSize;
        }

        /// <summary>指定武器の弾倉残弾を設定する。未記録なら追加し、負値は 0 にクランプして Dirty にする。</summary>
        public void SetMagazineCount(int weaponId, int count)
        {
            var data = _repository.Data?.Equipment;
            if (data == null)
                return;

            data.Magazines ??= new List<HorrorWeaponMagazineData>();

            foreach (var rec in data.Magazines)
            {
                if (rec.WeaponId == weaponId)
                {
                    rec.Count = Mathf.Max(0, count);
                    _repository.MarkDirty();
                    return;
                }
            }

            data.Magazines.Add(new HorrorWeaponMagazineData { WeaponId = weaponId, Count = Mathf.Max(0, count) });
            _repository.MarkDirty();
        }
    }
}
