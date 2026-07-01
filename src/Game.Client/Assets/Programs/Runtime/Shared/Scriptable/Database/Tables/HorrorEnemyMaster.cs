using System;
using UnityEngine;

namespace Game.Shared.Scriptable.Database.Tables
{
    /// <summary>
    /// ホラーゲームのゾンビ型敵 AI の調整値マスターデータ
    /// </summary>
    [Serializable]
    [ScriptableTable(Name = "Scriptable Database/Table/HorrorEnemyMasterTable")]
    public partial class HorrorEnemyMaster
    {
        #region SerializeField

        [SerializeField] private int _id;                          // 識別ID
        [SerializeField] private string _name;                     // 識別名

        [SerializeField] private float _walkSpeed;                 // 徘徊速度
        [SerializeField] private float _chaseSpeed;                // 追尾速度

        [SerializeField] private float _sightRange;                // 視程
        [SerializeField] private float _sightHalfAngle;            // 視野半角（度）
        [SerializeField] private float _eyeHeight;                 // 目の高さ

        [SerializeField] private float _hearingRadius;             // 聴覚基準半径
        [SerializeField] private float _hearingSensitivity;        // 聴覚感度倍率

        [SerializeField] private float _awarenessFillRate;         // 警戒度充填レート（/秒）
        [SerializeField] private float _awarenessDecayRate;        // 警戒度減衰レート（/秒）
        [SerializeField] private float _suspiciousThreshold;       // 警戒状態への閾値（0〜1）
        [SerializeField] private float _alertThreshold;            // 発見状態への閾値（0〜1）

        [SerializeField] private float _attackRange;               // 攻撃間合い
        [SerializeField] private int _attackDamage;                // 攻撃力
        [SerializeField] private float _attackCooldown;            // 攻撃間隔（秒）

        [SerializeField] private float _investigateGiveUpTime;     // 捜索を諦めるまでの秒数
        [SerializeField] private float _repathInterval;            // 目的地再計算間隔（秒）

        [SerializeField] private int _maxHealth;                   // 最大体力
        [SerializeField] private float _staggerDuration;           // のけぞり持続時間（秒）

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

        public float WalkSpeed
        {
            get => _walkSpeed;
            set => _walkSpeed = value;
        }

        public float ChaseSpeed
        {
            get => _chaseSpeed;
            set => _chaseSpeed = value;
        }

        public float SightRange
        {
            get => _sightRange;
            set => _sightRange = value;
        }

        public float SightHalfAngle
        {
            get => _sightHalfAngle;
            set => _sightHalfAngle = value;
        }

        public float EyeHeight
        {
            get => _eyeHeight;
            set => _eyeHeight = value;
        }

        public float HearingRadius
        {
            get => _hearingRadius;
            set => _hearingRadius = value;
        }

        public float HearingSensitivity
        {
            get => _hearingSensitivity;
            set => _hearingSensitivity = value;
        }

        public float AwarenessFillRate
        {
            get => _awarenessFillRate;
            set => _awarenessFillRate = value;
        }

        public float AwarenessDecayRate
        {
            get => _awarenessDecayRate;
            set => _awarenessDecayRate = value;
        }

        public float SuspiciousThreshold
        {
            get => _suspiciousThreshold;
            set => _suspiciousThreshold = value;
        }

        public float AlertThreshold
        {
            get => _alertThreshold;
            set => _alertThreshold = value;
        }

        public float AttackRange
        {
            get => _attackRange;
            set => _attackRange = value;
        }

        public int AttackDamage
        {
            get => _attackDamage;
            set => _attackDamage = value;
        }

        public float AttackCooldown
        {
            get => _attackCooldown;
            set => _attackCooldown = value;
        }

        public float InvestigateGiveUpTime
        {
            get => _investigateGiveUpTime;
            set => _investigateGiveUpTime = value;
        }

        public float RepathInterval
        {
            get => _repathInterval;
            set => _repathInterval = value;
        }

        public int MaxHealth
        {
            get => _maxHealth;
            set => _maxHealth = value;
        }

        public float StaggerDuration
        {
            get => _staggerDuration;
            set => _staggerDuration = value;
        }

        #endregion
    }
}
