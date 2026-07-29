using System;
using UnityEngine;

namespace Game.Shared.Scriptable.Database.Tables
{
    /// <summary>
    /// ホラーゲームのエネミースポーングループのマスターデータ。
    /// スポーングループは連鎖スポーンの単位で、全滅・グループ内キル数の到達をトリガーに別グループを起動する。
    /// </summary>
    [Serializable]
    [ScriptableTable(Name = "Scriptable Database/Table/HorrorEnemySpawnGroupMasterTable")]
    public partial class HorrorEnemySpawnGroupMaster
    {
        #region SerializeField

        [SerializeField] private int _id;                       // 識別ID（スポーングループの一意識別子）
        [SerializeField] private string _developOnlyName;       // 開発時のみの識別名
        [SerializeField] private bool _isInitialSpawn;          // シーン開始時にスポーンする初期グループか
        [SerializeField] private int _nextGroupIdOnEliminated;  // 全滅時に起動するグループ（0=なし）
        [SerializeField] private int _additionalKillThreshold;  // 追加グループを起動するグループ内キル数（0=なし）
        [SerializeField] private int _additionalGroupId;        // キル数到達で起動するグループ（0=なし）

        #endregion

        #region Database

        [PrimaryKey]
        public int Id
        {
            get => _id;
            set => _id = value;
        }

        public bool IsInitialSpawn
        {
            get => _isInitialSpawn;
            set => _isInitialSpawn = value;
        }

        public int NextGroupIdOnEliminated
        {
            get => _nextGroupIdOnEliminated;
            set => _nextGroupIdOnEliminated = value;
        }

        public int AdditionalKillThreshold
        {
            get => _additionalKillThreshold;
            set => _additionalKillThreshold = value;
        }

        public int AdditionalGroupId
        {
            get => _additionalGroupId;
            set => _additionalGroupId = value;
        }

        #endregion
    }
}
