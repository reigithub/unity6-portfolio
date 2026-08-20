using System.Collections.Generic;
using Game.Horror.SaveData;
using Game.Shared.Enums;
using Game.Shared.Scriptable.Database.Tables;
using Game.Shared.Services.Interfaces;
using R3;

namespace Game.Horror.Services.Interfaces
{
    /// <summary>
    /// Horror 装備状態（装備中武器＋ショートカット4枠）を扱うドメインサービスのインターフェース。
    /// </summary>
    public interface IHorrorEquipmentService : IGameService
    {
        /// <summary>装備・ショートカットスロットが変化したときに通知する</summary>
        Observable<Unit> EquipmentChanged { get; }

        /// <summary>指定 (SlotType, Id) が装備可能か判定する。装備対象は Weapon のみで、かつ所持している必要がある。</summary>
        bool CanEquip(ObjectCategory type, int id);

        /// <summary>
        /// 指定 (SlotType, Id) を装備状態にする。<see cref="CanEquip"/> が成立し、かつマスターを解決できる場合のみ
        /// 反映して Dirty にする。マスターを解決できない場合は装備を成立させない（LogError の上 false）。
        /// </summary>
        bool TryEquip(ObjectCategory type, int id);

        /// <summary>現在装備中の (SlotType, Id) を取得する。未装備または未ロードなら false。</summary>
        bool TryGetEquipped(out ObjectCategory type, out int id);

        /// <summary>装備を解除して素手に戻す（投げ切り等）。未装備・未ロードでも安全（冪等）。</summary>
        void Unequip();

        /// <summary>装備中武器のマスター（null = 未装備。<see cref="ResolveEquippedWeaponMaster"/> 前も null）。</summary>
        HorrorWeaponMaster EquippedWeaponMaster { get; }

        /// <summary>
        /// セーブデータの装備記録から装備中武器のマスターを確定する。セーブのロード・新規作成後、
        /// <see cref="EquippedWeaponMaster"/> を読む前に呼ぶ。
        /// 解決できない記録は不変条件違反として LogError の上、未装備へ戻す。
        /// </summary>
        void ResolveEquippedWeaponMaster();

        /// <summary>ショートカット登録＋装備中の武器をマスター解決し、同一 Id を重複排除して列挙する（スロット0→3→装備中の順）。</summary>
        List<HorrorWeaponMaster> GetEquippableWeaponMasters();

        /// <summary>指定スロット(0-3)へアイテム (SlotType, Id) を登録する。</summary>
        bool TrySetSlot(int index, ObjectCategory slotType, int id);

        /// <summary>対象アイテムを destIndex に割り当てる。既登録なら移動/入替、未登録なら上書きする。</summary>
        bool TryAssignSlot(int destIndex, ObjectCategory slotType, int id);

        /// <summary>指定アイテムを最初の空きスロット（index 昇順）へ登録する。既登録・空き無し・未ロードは何もせず false。</summary>
        bool TryAutoAssignSlot(ObjectCategory slotType, int id);

        /// <summary>指定スロット(0-3)の登録を外す（空にする）。</summary>
        bool ClearSlot(int index);

        /// <summary>指定アイテム (SlotType, Id) を登録しているスロットを空にする（枯渇時の登録除去用）。未登録・未ロードは何もせず false（冪等）。</summary>
        bool TryClearSlotOf(ObjectCategory category, int id);

        /// <summary>指定スロットの登録を取得する。空(None)または範囲外なら false。</summary>
        bool TryGetSlot(int index, out HorrorEquipmentSlotData slot);

        /// <summary>指定スロットの入力方向を文字列で取得する</summary>
        string GetSlotInputDirection(ObjectCategory category, int id);

        /// <summary>指定武器の弾倉残弾を取得する。未記録・未ロードなら満タン（magazineSize）を返す。</summary>
        int GetMagazineCount(int weaponId, int magazineSize);

        /// <summary>指定武器の弾倉残弾を設定する。未記録なら追加し、負値は 0 にクランプして Dirty にする。</summary>
        void SetMagazineCount(int weaponId, int count);
    }
}
