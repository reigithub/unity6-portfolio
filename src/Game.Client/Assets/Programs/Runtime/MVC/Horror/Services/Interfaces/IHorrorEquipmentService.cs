using Game.Core.Services;
using Game.Horror.SaveData;
using Game.Shared.Enums;

namespace Game.Horror.Services.Interfaces
{
    /// <summary>
    /// Horror 装備状態（装備中武器＋ショートカット4枠）を扱うドメインサービスのインターフェース。
    /// </summary>
    public interface IHorrorEquipmentService : IGameService
    {
        /// <summary>指定 (SlotType, Id) が装備可能か判定する。装備対象は Weapon のみで、かつ所持している必要がある。</summary>
        bool CanEquip(InventorySlotType type, int id);

        /// <summary>指定 (SlotType, Id) を装備状態にする。<see cref="CanEquip"/> が成立する場合のみ反映して Dirty にする。</summary>
        bool TryEquip(InventorySlotType type, int id);

        /// <summary>現在装備中の (SlotType, Id) を取得する。未装備または未ロードなら false。</summary>
        bool TryGetEquipped(out InventorySlotType type, out int id);

        /// <summary>指定スロット(0-3)へアイテム (SlotType, Id) を登録する。</summary>
        bool TrySetSlot(int index, InventorySlotType slotType, int id);

        /// <summary>対象アイテムを destIndex に割り当てる。既登録なら移動/入替、未登録なら上書きする。</summary>
        bool TryAssignSlot(int destIndex, InventorySlotType slotType, int id);

        /// <summary>指定スロット(0-3)の登録を外す（空にする）。</summary>
        bool ClearSlot(int index);

        /// <summary>指定スロットの登録を取得する。空(None)または範囲外なら false。</summary>
        bool TryGetSlot(int index, out HorrorEquipmentSlotData slot);

        /// <summary>指定武器の弾倉残弾を取得する。未記録・未ロードなら満タン（magazineSize）を返す。</summary>
        int GetMagazineCount(int weaponId, int magazineSize);

        /// <summary>指定武器の弾倉残弾を設定する。未記録なら追加し、負値は 0 にクランプして Dirty にする。</summary>
        void SetMagazineCount(int weaponId, int count);
    }
}
