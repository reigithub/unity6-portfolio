using System;
using Game.Shared.Enums;
using Game.Shared.Scriptable.Database.Validation;
using UnityEngine;

namespace Game.Shared.Scriptable.Database.Tables
{
    /// <summary>
    /// クラフトレシピが要求する素材の 1 件。<see cref="MaterialGroupId"/> 単位でまとめて 1 レシピ分の素材になる。
    /// <see cref="HorrorCraftMaster.MaterialGroupId"/> からグループキーで参照される。
    /// 素材は種別（Item/Weapon）と Id の組で指すため、参照整合性は宣言属性ではなく
    /// 専用バリデータ（HorrorCraftMaterialObjectValidator）が担う。
    /// </summary>
    [Serializable]
    [ScriptableTable(Name = "Scriptable Database/Table/HorrorCraftMaterialMasterTable")]
    public partial class HorrorCraftMaterialMaster
    {
        #region SerializeField

        [SerializeField] private int _id;                        // 識別ID（素材エントリの一意識別子）
        [SerializeField] private string _developOnlyName;        // 開発時のみの識別名
        [SerializeField] private int _materialGroupId;           // 素材グループ（HorrorCraftMaster.MaterialGroupId から参照される）
        [SerializeField] private ObjectCategory _objectCategory; // 素材の種別
        [SerializeField] private int _objectId;                  // 素材（種別に対応するマスターの Id）
        [SerializeField] private int _count;                     // 消費数量

        #endregion

        #region Database

        [PrimaryKey]
        public int Id
        {
            get => _id;
            set => _id = value;
        }

        [SecondaryKey(0, nonUnique: true)]
        [ValueRange(1, int.MaxValue)]
        public int MaterialGroupId
        {
            get => _materialGroupId;
            set => _materialGroupId = value;
        }

        /// <summary>素材の種別。<see cref="ObjectCategory.None"/> は不正。</summary>
        public ObjectCategory ObjectCategory
        {
            get => _objectCategory;
            set => _objectCategory = value;
        }

        /// <summary>素材の Id（<see cref="ObjectCategory"/> に対応するマスターの主キー）。</summary>
        public int ObjectId
        {
            get => _objectId;
            set => _objectId = value;
        }

        /// <summary>1 回のクラフトで消費する数量。</summary>
        [ValueRange(1, 999)]
        public int Count
        {
            get => _count;
            set => _count = value;
        }

        #endregion
    }
}
