using System;

namespace Game.Shared.Enums
{
    public enum InventorySlotType
    {
        None = 0,
        Item = 1,
        Weapon = 2,
    }

    /// <summary>
    /// インベントリのコンテキストサブメニューで選択できるアクション種別。
    /// </summary>
    public enum InventoryContextActionType
    {
        Use,
        Inspect,
        Discard,
        Equip,
        Shortcut,
    }

    /// <summary>
    /// スロット種別ごとにコンテキストサブメニューの表示エントリと表示ラベルを解決する純粋ヘルパー。
    /// 副作用を持たないためユニットテスト対象。
    /// </summary>
    public static partial class InventorySlotTypeExtensions
    {
        private static readonly InventoryContextActionType[] _itemActions =
        {
            InventoryContextActionType.Use,
            InventoryContextActionType.Inspect,
            InventoryContextActionType.Discard,
        };

        private static readonly InventoryContextActionType[] _weaponActions =
        {
            InventoryContextActionType.Equip,
            InventoryContextActionType.Shortcut,
            InventoryContextActionType.Inspect,
        };

        private static readonly InventoryContextActionType[] _empty = Array.Empty<InventoryContextActionType>();

        /// <summary>スロット種別に対応するアクション列を返す。未対応種別は空。</summary>
        public static InventoryContextActionType[] ToContextActions(this InventorySlotType slotType)
        {
            switch (slotType)
            {
                case InventorySlotType.Item:
                    return _itemActions;
                case InventorySlotType.Weapon:
                    return _weaponActions;
                default:
                    return _empty;
            }
        }
    }

}
