using System;
using Game.Shared.Interfaces;

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

    public static partial class ObjectInfoExtensions
    {
        private static readonly ContextActionType[] _effectiveItemActions =
        {
            ContextActionType.Use,
            ContextActionType.Inspect,
            ContextActionType.Discard,
        };

        private static readonly ContextActionType[] _itemActions =
        {
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
        public static ContextActionType[] ToContextActions(this IObjectInfo info)
        {
            switch (info.ObjectCategory)
            {
                case ObjectCategory.Item:
                    return info.HasEffect ? _effectiveItemActions : _itemActions;
                case ObjectCategory.Weapon:
                    return _weaponActions;
                default:
                    return _empty;
            }
        }
    }
}
