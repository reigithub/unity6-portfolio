using System.Collections.Generic;
using Game.Horror.Constants;
using Game.Horror.SaveData;
using Game.Horror.Services.Interfaces;
using Game.Shared.Enums;
using Game.Shared.Scriptable.Database.Tables;
using Game.Shared.Services;
using R3;
using UnityEngine;

namespace Game.Horror.Services
{
    /// <summary>
    /// Horror 装備状態を扱うドメインサービス。装備中武器の保持と、装備ショートカット(D-Pad 4スロット)の登録・整合を合わせて担う。
    /// </summary>
    public class HorrorEquipmentService : IHorrorEquipmentService
    {
        /// <summary>ショートカットスロット数（D-Pad 1〜4）。</summary>
        private const int MaxEquipmentSlotCount = HorrorEquipmentConstants.MaxEquipmentSlotCount;

        private readonly IHorrorSaveRepository _repository;
        private readonly IHorrorInventoryService _inventoryService;
        private readonly IScriptableDatabaseService _databaseService;

        private HorrorWeaponMaster _equippedWeaponMaster;

        public HorrorEquipmentService(IHorrorSaveRepository repository, IHorrorInventoryService inventoryService, IScriptableDatabaseService databaseService)
        {
            _repository = repository;
            _inventoryService = inventoryService;
            _databaseService = databaseService;
        }

        /// <summary>
        /// 指定 (SlotType, Id) が装備可能か判定する。装備対象は Weapon のみで、かつ所持している必要がある。
        /// </summary>
        public bool CanEquip(ObjectCategory type, int id) => type == ObjectCategory.Weapon && _inventoryService.HasObject(type, id);

        /// <summary>
        /// 指定 (SlotType, Id) を装備状態にする。<see cref="CanEquip"/> が成立し、かつマスターを解決できる場合のみ
        /// 反映して Dirty にする。現在と同一の装備を指定した場合も冪等に true を返す。
        /// </summary>
        public bool TryEquip(ObjectCategory type, int id)
        {
            var data = _repository.Data?.Equipment;
            if (data == null || !CanEquip(type, id))
                return false;

            // 記録とマスターの確定は同時に行う（装備中なのにマスターが無い状態を作らない）
            if (!_databaseService.Database.HorrorWeaponMasterTable.TryFindById(id, out var master))
            {
                Debug.LogError($"装備中の武器マスターが見つかりません Id={id}");
                return false;
            }

            data.ObjectCategory = type;
            data.Id = id;
            _equippedWeaponMaster = master;
            _repository.MarkDirty();
            return true;
        }

        /// <summary>現在装備中の (SlotType, Id) を取得する。未装備または未ロードなら false。</summary>
        public bool TryGetEquipped(out ObjectCategory type, out int id)
        {
            type = ObjectCategory.None;
            id = 0;

            var data = _repository.Data?.Equipment;
            if (data == null || data.ObjectCategory == ObjectCategory.None)
                return false;

            type = data.ObjectCategory;
            id = data.Id;
            return true;
        }

        /// <summary>装備中武器のマスター（null = 未装備。<see cref="ResolveEquippedWeaponMaster"/> 前も null）。</summary>
        public HorrorWeaponMaster EquippedWeaponMaster => _equippedWeaponMaster;

        /// <summary>
        /// セーブデータの装備記録からマスターを確定する。プレイ開始時（セーブのロード・新規作成後）に呼ぶ。
        /// ロード時は NormalizeEquipment がマスター不在装備を None へ正規化済みのため、解決失敗＝不変条件違反。
        /// </summary>
        public void ResolveEquippedWeaponMaster()
        {
            _equippedWeaponMaster = null;

            var data = _repository.Data?.Equipment;
            if (data == null || data.ObjectCategory != ObjectCategory.Weapon)
                return; // 未ロード・未装備

            if (_databaseService.Database.HorrorWeaponMasterTable.TryFindById(data.Id, out _equippedWeaponMaster))
                return;

            // 記録と実体の乖離を残さない（次回ロード時の NormalizeEquipment と同じ結果へ寄せる）
            Debug.LogError($"装備中の武器マスターが見つかりません Id={data.Id}。未装備へ戻します");
            data.ObjectCategory = ObjectCategory.None;
            data.Id = 0;
            _repository.MarkDirty();
        }

        /// <summary>
        /// ショートカット登録＋装備中の武器をマスター解決し、同一 Id を重複排除して列挙する（スロット0→3→装備中の順）。
        /// マスター未解決のスロット登録は無音でスキップする（ロード時正規化で通常は発生しない）。
        /// </summary>
        public List<HorrorWeaponMaster> GetEquippableWeaponMasters()
        {
            var masters = new List<HorrorWeaponMaster>();

            for (var i = 0; i < MaxEquipmentSlotCount; i++)
            {
                if (TryGetSlot(i, out var slot)
                    && slot.ObjectCategory == ObjectCategory.Weapon
                    && !masters.Exists(m => m.Id == slot.Id)
                    && _databaseService.Database.HorrorWeaponMasterTable.TryFindById(slot.Id, out var slotMaster))
                {
                    masters.Add(slotMaster);
                }
            }

            var equippedWeapon = EquippedWeaponMaster;
            if (equippedWeapon != null && !masters.Exists(m => m.Id == equippedWeapon.Id))
            {
                masters.Add(equippedWeapon);
            }

            return masters;
        }

        /// <summary>指定スロット(0-3)へアイテム (SlotType, Id) を登録する。</summary>
        public bool TrySetSlot(int index, ObjectCategory slotType, int id)
        {
            var data = _repository.Data?.Equipment;
            if (data == null || index < 0 || index >= MaxEquipmentSlotCount)
                return false;

            var slot = data.Slots[index];
            slot.ObjectCategory = slotType;
            slot.Id = id;
            _repository.MarkDirty();
            return true;
        }

        /// <summary>
        /// 対象アイテムを destIndex に割り当てる。同一アイテムが既に別スロットにあれば旧スロットと内容を交換
        /// （交換先が空なら実質「移動」）、無ければ上書き。単一登録（同一アイテムは高々1スロット）を保つ。
        /// </summary>
        public bool TryAssignSlot(int destIndex, ObjectCategory slotType, int id)
        {
            var data = _repository.Data?.Equipment;
            if (data == null || destIndex < 0 || destIndex >= MaxEquipmentSlotCount)
                return false;

            int index = GetSlotIndex(data, slotType, id);
            if (index == destIndex)
                return false; // 既に同じスロット → 変化なし

            var dest = data.Slots[destIndex];
            if (index >= 0)
            {
                // 既登録 → 旧スロットへ dest の旧内容を移す（dest が空なら旧が空になり「移動」、占有なら入替）
                var src = data.Slots[index];
                src.ObjectCategory = dest.ObjectCategory;
                src.Id = dest.Id;
            }

            // dest に対象を置く（未登録時は上書き）
            dest.ObjectCategory = slotType;
            dest.Id = id;
            _repository.MarkDirty();
            return true;
        }

        /// <summary>
        /// 指定アイテムを最初の空きスロット（index 昇順）へ登録する。
        /// 既登録なら何もしない（単一登録の不変条件を維持）。空き無し・未ロードも何もせず false。
        /// </summary>
        public bool TryAutoAssignSlot(ObjectCategory slotType, int id)
        {
            var data = _repository.Data?.Equipment;
            if (data == null || slotType == ObjectCategory.None)
                return false;

            if (GetSlotIndex(data, slotType, id) >= 0)
                return false;

            for (var i = 0; i < MaxEquipmentSlotCount; i++)
            {
                if (!TryGetSlot(i, out _))
                    return TrySetSlot(i, slotType, id);
            }

            return false; // 空き無し
        }

        // 指定アイテム (SlotType, Id) が登録されているスロット index を返す（None は対象外）。無ければ -1。
        private static int GetSlotIndex(HorrorEquipmentSaveData data, ObjectCategory slotType, int id)
        {
            if (data == null || slotType == ObjectCategory.None)
                return -1;

            for (int i = 0; i < MaxEquipmentSlotCount; i++)
            {
                var s = data.Slots[i];
                if (s.ObjectCategory == slotType && s.Id == id)
                    return i;
            }
            return -1;
        }

        /// <summary>指定スロット(0-3)の登録を外す（空にする）。</summary>
        public bool ClearSlot(int index)
        {
            var data = _repository.Data?.Equipment;
            if (data == null || index < 0 || index >= MaxEquipmentSlotCount)
                return false;

            var slot = data.Slots[index];
            slot.ObjectCategory = ObjectCategory.None;
            slot.Id = 0;
            _repository.MarkDirty();
            return true;
        }

        /// <summary>指定スロットの登録を取得する。空(None)または範囲外なら false。</summary>
        public bool TryGetSlot(int index, out HorrorEquipmentSlotData slot)
        {
            slot = null;
            var data = _repository.Data?.Equipment;
            if (data == null || index < 0 || index >= MaxEquipmentSlotCount)
                return false;

            var s = data.Slots[index];
            if (s.ObjectCategory == ObjectCategory.None)
                return false;

            slot = s;
            return true;
        }

        private bool TryGetSlot(ObjectCategory category, int id, out HorrorEquipmentSlotData slot, out int index)
        {
            slot = null;
            index = -1;
            var data = _repository.Data?.Equipment;
            if (data == null)
                return false;

            for (int i = 0; i < data.Slots.Count; i++)
            {
                var s = data.Slots[i];
                if (s.ObjectCategory == category && s.Id == id)
                {
                    slot = s;
                    index = i;
                    return true;
                }
            }

            return false;
        }

        public string GetSlotInputDirection(ObjectCategory category, int id)
        {
            if (!TryGetSlot(category, id, out _, out int index))
                return string.Empty;

            if (!HorrorEquipmentConstants.SlotInputDirections.TryGetValue(index, out string direction))
                return string.Empty;

            return direction;
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
