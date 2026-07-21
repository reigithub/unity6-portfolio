using System;

namespace Game.Shared.Enums
{
    public enum ObjectCategory
    {
        None = 0,
        Item = 1,
        Weapon = 2,
    }

    /// <summary>
    /// インベントリのコンテキストサブメニューで選択できるアクション種別。
    /// </summary>
    public enum ContextActionType
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
        private static readonly ContextActionType[] _itemActions =
        {
            ContextActionType.Use,
            ContextActionType.Inspect,
            ContextActionType.Discard,
        };

        private static readonly ContextActionType[] _weaponActions =
        {
            ContextActionType.Equip,
            ContextActionType.Shortcut,
            ContextActionType.Inspect,
        };

        private static readonly ContextActionType[] _empty = Array.Empty<ContextActionType>();

        /// <summary>スロット種別に対応するアクション列を返す。未対応種別は空。</summary>
        public static ContextActionType[] ToContextActions(this ObjectCategory slotType)
        {
            switch (slotType)
            {
                case ObjectCategory.Item:
                    return _itemActions;
                case ObjectCategory.Weapon:
                    return _weaponActions;
                default:
                    return _empty;
            }
        }
    }

}
