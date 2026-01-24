using System;
using Game.Library.Shared.MasterData.MemoryTables;
using Game.Shared.Combat;
using Game.Shared.Events;
using Game.Shared.Extensions;
using R3;
using UnityEngine;
using UnityEngine.AI;

namespace Game.MVP.Survivor.Enemy
{
    /// <summary>
    /// Survivor敵コントローラー
    /// マスターデータから初期化され、StateMachineでAI制御
    /// </summary>
    public partial class SurvivorEnemyController : MonoBehaviour, ICombatTarget, IDeathNotifier
    {
        [Header("Components")]
        [SerializeField] private NavMeshAgent _navAgent;

        [SerializeField] private Animator _animator;
        [SerializeField] private Collider _collider;

        // マスターデータから設定される値
        private int _enemyId;
        private int _enemyType;
        private int _maxHp;
        private int _attackDamage;
        private int _experienceValue;
        private float _moveSpeed;
        private float _attackRange;
        private float _attackCooldown;
        private float _hitStunDuration;
        private float _rotationSpeed;
        private float _deathAnimDuration;
        private int _itemDropGroupId;
        private int _expDropGroupId;

        // State
        private int _currentHp;
        private Transform _target;
        private bool _isDead;

        // Events
        private readonly Subject<SurvivorEnemyController> _onDeath = new();
        public Observable<SurvivorEnemyController> OnDeath => _onDeath;

        // IDeathNotifier implementation
        private readonly Subject<DeathEventData> _onDeathEvent = new();
        public Observable<DeathEventData> OnDeathEvent => _onDeathEvent;

        // Public properties
        public int EnemyId => _enemyId;

        /// <summary>敵タイプ（1:通常, 2:エリート, 3:ボス）</summary>
        public int EnemyType => _enemyType;

        public bool IsBoss => _enemyType == 3;
        public int AttackDamage => _attackDamage;
        public int ExperienceValue => _experienceValue;
        public bool IsDead => _isDead;

        /// <summary>死亡アニメーション時間（秒）</summary>
        public float DeathAnimDuration => _deathAnimDuration;

        /// <summary>アイテムドロップグループID（0=ドロップなし）</summary>
        public int ItemDropGroupId => _itemDropGroupId;

        /// <summary>経験値ドロップグループID（0=ドロップなし）</summary>
        public int ExpDropGroupId => _expDropGroupId;

        /// <summary>
        /// エンティティの中心位置（コライダーの中心）
        /// </summary>
        public Vector3 CenterPosition => _collider != null
            ? _collider.bounds.center
            : transform.position;

        // Animator hashes
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int DeathHash = Animator.StringToHash("Death");
        private static readonly int HitHash = Animator.StringToHash("Hit");

        private void Awake()
        {
            if (_navAgent == null)
            {
                _navAgent = GetComponent<NavMeshAgent>();
            }

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            if (_collider == null)
            {
                _collider = GetComponentInChildren<Collider>();
            }
        }

        /// <summary>
        /// マスターデータから初期化
        /// </summary>
        public void Initialize(
            SurvivorEnemyMaster master,
            Transform target,
            float speedMultiplier = 1f,
            float healthMultiplier = 1f,
            float damageMultiplier = 1f,
            float experienceMultiplier = 1f,
            int itemDropGroupId = 0,
            int expDropGroupId = 0)
        {
            _enemyId = master.Id;
            _enemyType = master.EnemyType;
            _target = target;
            _itemDropGroupId = itemDropGroupId;
            _expDropGroupId = expDropGroupId;

            // マスターデータからパラメータ設定（倍率適用）
            _maxHp = Mathf.RoundToInt(master.BaseHp * healthMultiplier);
            _attackDamage = Mathf.RoundToInt(master.BaseDamage * damageMultiplier);
            _experienceValue = Mathf.RoundToInt(master.ExperienceValue * experienceMultiplier);
            _moveSpeed = master.MoveSpeed.ToUnit() * speedMultiplier;

            // 戦闘パラメータ
            _attackRange = master.AttackRange.ToUnit();
            _attackCooldown = master.AttackCooldown.ToSeconds();
            _hitStunDuration = master.HitStunDuration.ToSeconds();
            _rotationSpeed = master.RotationSpeed;
            _deathAnimDuration = master.DeathAnimDuration.ToSeconds();

            _currentHp = _maxHp;
            _isDead = false;

            if (_navAgent != null)
            {
                _navAgent.speed = _moveSpeed;
                _navAgent.enabled = true;
            }

            if (_collider != null)
            {
                _collider.enabled = true;
            }

            InitializeStateMachine();
        }

        private void Update()
        {
            _stateMachine?.Update();
        }

        public void TakeDamage(int damage)
        {
            TakeDamageWithStateMachine(damage);
        }

        public void ApplyKnockback(Vector3 knockback)
        {
            if (_isDead || _navAgent == null || !_navAgent.enabled) return;

            // NavMeshAgentのvelocityにノックバックを適用
            _navAgent.velocity = knockback;
        }

        /// <summary>
        /// プールに戻すためのリセット
        /// </summary>
        public void ResetForPool()
        {
            _isDead = false;
            _currentHp = _maxHp;
            _target = null;
            _stateMachine = null;
            _damageableTarget = null;

            if (_navAgent != null)
            {
                _navAgent.enabled = false;
            }

            if (_collider != null)
            {
                _collider.enabled = true;
            }

            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            _onDeath.Dispose();
            _onDeathEvent.Dispose();
        }
    }
}