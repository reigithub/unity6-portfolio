using System;
using Game.Shared.Scriptable.Database.Validation;
using UnityEngine;

namespace Game.Shared.Scriptable.Database.Tables
{
    /// <summary>
    /// エネミー撃破時ドロップの抽選テーブル。
    /// DropGroupId 単位で累積抽選し、グループ内 DropRate 合計（万分率）の 10000 に対する不足分がドロップなしになる。
    /// HorrorEnemyMaster.DropGroupId からグループキーで参照される。
    /// </summary>
    [Serializable]
    [ScriptableTable(Name = "Scriptable Database/Table/HorrorEnemyDropMasterTable")]
    public partial class HorrorEnemyDropMaster
    {
        #region SerializeField

        [SerializeField] private int _id;                  // 識別ID（ドロップエントリの一意識別子）
        [SerializeField] private string _developOnlyName;  // 開発時のみの識別名
        [SerializeField] private int _dropGroupId;         // 抽選グループ（HorrorEnemyMaster.DropGroupId から参照される）
        [SerializeField] private int _itemId;              // ドロップするアイテム（HorrorItemMaster の Id）
        [SerializeField] private int _dropRate;            // ドロップ率（万分率。10000 = 100%）
        [SerializeField] private int _count;               // 付与数量

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
        public int DropGroupId
        {
            get => _dropGroupId;
            set => _dropGroupId = value;
        }

        [ForeignKey(typeof(HorrorItemMaster))]
        public int ItemId
        {
            get => _itemId;
            set => _itemId = value;
        }

        [ValueRange(1, 10000)]
        public int DropRate
        {
            get => _dropRate;
            set => _dropRate = value;
        }

        [ValueRange(1, 999)]
        public int Count
        {
            get => _count;
            set => _count = value;
        }

        #endregion
    }
}
