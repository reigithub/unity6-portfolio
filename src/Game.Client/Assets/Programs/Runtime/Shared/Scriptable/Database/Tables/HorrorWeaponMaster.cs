using System;
using UnityEngine;

namespace Game.Shared.Scriptable.Database.Tables
{
    /// <summary>
    /// ホラーゲームのプレイヤー武器（ハンドガン等）の調整値マスターデータ
    /// </summary>
    [Serializable]
    [ScriptableTable(Name = "Scriptable Database/Table/HorrorWeaponMasterTable")]
    public partial class HorrorWeaponMaster
    {
        #region SerializeField

        [SerializeField] private int _id;                  // 識別ID
        [SerializeField] private string _name;             // 識別名

        [SerializeField] private int _damage;              // 着弾ダメージ（IDamageable.TakeDamage へ渡す）
        [SerializeField] private float _range;             // 射程（Raycast 最大距離, m）
        [SerializeField] private float _fireInterval;      // 発砲後の硬直（AttackingState 滞在秒）
        [SerializeField] private float _noiseLoudness;     // 銃声の大きさ（NoiseEvent.Loudness）

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

        public int Damage
        {
            get => _damage;
            set => _damage = value;
        }

        public float Range
        {
            get => _range;
            set => _range = value;
        }

        public float FireInterval
        {
            get => _fireInterval;
            set => _fireInterval = value;
        }

        public float NoiseLoudness
        {
            get => _noiseLoudness;
            set => _noiseLoudness = value;
        }

        #endregion
    }
}
