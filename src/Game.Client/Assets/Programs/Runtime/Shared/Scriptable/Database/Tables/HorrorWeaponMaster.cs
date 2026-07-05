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

        [SerializeField] private int _id;
        [SerializeField] private string _name;
        [SerializeField] private string _description;
        [SerializeField] private string _iconAssetName;
        [SerializeField] private string _modelAssetName;
        [SerializeField] private int _maxCount;

        [SerializeField] private int _damage;              // 着弾ダメージ（IDamageable.TakeDamage へ渡す）
        [SerializeField] private float _range;             // 射程（Raycast 最大距離, m）
        [SerializeField] private float _fireInterval;      // 発砲後の硬直（AttackingState 滞在秒）
        [SerializeField] private float _noiseLoudness;     // 銃声の大きさ（NoiseEvent.Loudness）
        [SerializeField] private float _equipDuration;     // 装備切替の硬直（EquippingState 滞在秒）

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

        public string ModelAssetName
        {
            get => _modelAssetName;
            set => _modelAssetName = value;
        }

        public int MaxCount
        {
            get => _maxCount;
            set => _maxCount = value;
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

        public float EquipDuration
        {
            get => _equipDuration;
            set => _equipDuration = value;
        }

        #endregion
    }
}
