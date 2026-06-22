using System;
using UnityEngine;

namespace Game.Shared.Scriptable.Database.Tables
{
    /// <summary>
    /// テーブルシステムの動作確認用サンプル。主キー＋単一二次キー＋複合二次キーを持つ。
    /// （Unity が List 要素としてシリアライズできるよう [Serializable]）
    /// </summary>
    [Serializable]
    [ScriptableTable(Name = "Scriptable Database/Table/WeaponLevelMasterTable")]
    public partial class WeaponLevelMaster
    {
        #region SerializeField

        [SerializeField] private int _id;
        [SerializeField] private int _weaponId;
        [SerializeField] private int _level;
        [SerializeField] private string _assetName;

        #endregion

        #region Database

        [PrimaryKey]
        public int Id
        {
            get => _id;
            set => _id = value;
        }

        [SecondaryKey(0, nonUnique: true)]   // WeaponId 単独: 非ユニーク
        [SecondaryKey(1, keyOrder: 0)]       // 複合 index1: ユニーク
        public int WeaponId
        {
            get => _weaponId;
            set => _weaponId = value;
        }

        [SecondaryKey(1, keyOrder: 1)]
        public int Level
        {
            get => _level;
            set => _level = value;
        }

        public string AssetName
        {
            get => _assetName;
            set => _assetName = value;
        }

        #endregion
    }
}
