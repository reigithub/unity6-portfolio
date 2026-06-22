using System;
using UnityEngine;

namespace Game.Shared.Scriptable.Database.Tables
{
    /// <summary>
    /// テーブルシステムの動作確認用サンプル。主キー＋単一二次キー＋複合二次キーを持つ。
    /// （Unity が List 要素としてシリアライズできるよう [Serializable]）
    /// </summary>
    [Serializable]
    [ScriptableTable(Name = "Scriptable Database/Table/HorrorItemMasterTable")]
    public partial class HorrorItemMaster
    {
        #region SerializeField

        [SerializeField] private int _id;
        [SerializeField] private string _name;
        [SerializeField] private string _description;
        [SerializeField] private int _maxQuantity;
        [SerializeField] private string _iconAssetName;

        #endregion

        #region Database

        [PrimaryKey]
        public int Id
        {
            get => _id;
            set => _id = value;
        }

        public string Name
        {
            get => _name;
            set => _name = value;
        }

        public string Description
        {
            get => _description;
            set => _description = value;
        }

        public int MaxQuantity
        {
            get => _maxQuantity;
            set => _maxQuantity = value;
        }

        public string IconAssetName
        {
            get => _iconAssetName;
            set => _iconAssetName = value;
        }

        #endregion
    }
}
