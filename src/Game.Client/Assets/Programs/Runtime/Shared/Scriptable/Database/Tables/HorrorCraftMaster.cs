using System;
using Game.Shared.Enums;
using Game.Shared.Scriptable.Database.Validation;
using UnityEngine;

namespace Game.Shared.Scriptable.Database.Tables
{
    /// <summary>
    /// クラフト（アイテム合成）のレシピ定義。1 行が 1 レシピで、生成物と素材グループを結ぶ。
    /// 素材は <see cref="MaterialGroupId"/> をグループキーに <see cref="HorrorCraftMaterialMaster"/> を引く（1 レシピ : N 素材）。
    /// 生成物は種別（Item/Weapon）と Id の組で指す。単一テーブルを指さないため参照整合性は宣言属性ではなく
    /// 専用バリデータ（HorrorCraftResultValidator）が担う。
    /// </summary>
    [Serializable]
    [ScriptableTable(Name = "Scriptable Database/Table/HorrorCraftMasterTable")]
    public partial class HorrorCraftMaster
    {
        #region SerializeField

        [SerializeField] private int _id;                              // 識別ID（レシピの一意識別子）
        [SerializeField] private string _developOnlyName;              // 開発時のみの識別名
        [SerializeField] private ObjectCategory _resultObjectCategory; // 生成物の種別
        [SerializeField] private int _resultObjectId;                  // 生成物（種別に対応するマスターの Id）
        [SerializeField] private int _resultCount;                     // 生成数量
        [SerializeField] private int _materialGroupId;                 // 素材グループ（HorrorCraftMaterialMaster.MaterialGroupId を引く）

        #endregion

        #region Database

        [PrimaryKey]
        public int Id
        {
            get => _id;
            set => _id = value;
        }

        /// <summary>生成物の種別。<see cref="ObjectCategory.None"/> は不正。</summary>
        public ObjectCategory ResultObjectCategory
        {
            get => _resultObjectCategory;
            set => _resultObjectCategory = value;
        }

        /// <summary>生成物の Id（<see cref="ResultObjectCategory"/> に対応するマスターの主キー）。</summary>
        public int ResultObjectId
        {
            get => _resultObjectId;
            set => _resultObjectId = value;
        }

        /// <summary>1 回のクラフトで生成する数量。</summary>
        [ValueRange(1, 999)]
        public int ResultCount
        {
            get => _resultCount;
            set => _resultCount = value;
        }

        /// <summary>素材グループ（<see cref="HorrorCraftMaterialMaster.MaterialGroupId"/> から素材行を引く）。</summary>
        [ValueRange(1, int.MaxValue)]
        public int MaterialGroupId
        {
            get => _materialGroupId;
            set => _materialGroupId = value;
        }

        #endregion
    }
}
