using System;
using UnityEngine;

namespace Game.Shared.Scriptable.Database.Tables
{
    [Serializable]
    [ScriptableTable(Name = "Scriptable Database/Table/HorrorItemMasterTable")]
    public partial class HorrorItemMaster
    {
        #region SerializeField

        [SerializeField] private int _id;
        [SerializeField] private string _name;
        [SerializeField] private string _description;
        [SerializeField] private string _iconAssetName;
        [SerializeField] private int _maxCount;

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

        public string IconAssetName
        {
            get => _iconAssetName;
            set => _iconAssetName = value;
        }

        public int MaxCount
        {
            get => _maxCount;
            set => _maxCount = value;
        }

        #endregion
    }
}
