using System;
using Game.Shared.Scriptable.Database.Validation;
using UnityEngine;

namespace Game.Shared.Scriptable.Database.Tables
{
    /// <summary>
    /// ホラーゲームのエネミースポーントリガーのマスターデータ。
    /// シーン上のトリガーボリューム通過（Id で対応付け）で起動するスポーングループを定義する。
    /// </summary>
    [Serializable]
    [ScriptableTable(Name = "Scriptable Database/Table/HorrorEnemySpawnTriggerMasterTable")]
    public partial class HorrorEnemySpawnTriggerMaster
    {
        #region SerializeField

        [SerializeField] private int _id;                  // 識別ID（トリガーの一意識別子。発火記録の永続化キー）
        [SerializeField] private string _developOnlyName;  // 開発時のみの識別名
        [SerializeField] private int _spawnGroupId;        // 通過時に起動するスポーングループ（HorrorEnemySpawnGroupMaster の Id。必須）

        #endregion

        #region Database

        [PrimaryKey]
        public int Id
        {
            get => _id;
            set => _id = value;
        }

        [ForeignKey(typeof(HorrorEnemySpawnGroupMaster))]
        public int SpawnGroupId
        {
            get => _spawnGroupId;
            set => _spawnGroupId = value;
        }

        #endregion
    }
}
