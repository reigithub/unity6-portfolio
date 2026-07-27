using System;
using UnityEngine;

namespace Game.Shared.Scriptable.Database.Tables
{
    /// <summary>
    /// ホラーゲームのエネミースポーンエントリのマスターデータ。
    /// Id はシーン配置1体分の一意識別子で、撃破記録（セーブデータ）の永続化キーになる。
    /// </summary>
    [Serializable]
    [ScriptableTable(Name = "Scriptable Database/Table/HorrorEnemySpawnMasterTable")]
    public partial class HorrorEnemySpawnMaster
    {
        #region SerializeField

        [SerializeField] private int _id;                  // 識別ID（スポーンエントリの一意識別子）
        [SerializeField] private string _developOnlyName;  // 開発時のみの識別名
        [SerializeField] private int _enemyMasterId;       // 生成する敵の種類（HorrorEnemyMaster の Id）

        #endregion

        #region Database

        [PrimaryKey]
        public int Id
        {
            get => _id;
            set => _id = value;
        }

        public int EnemyMasterId
        {
            get => _enemyMasterId;
            set => _enemyMasterId = value;
        }

        #endregion
    }
}
