using Game.Client.MasterData;
using Game.Shared.Combat;
using Game.Shared.Events;
using Game.Shared.Extensions;
using Game.Shared.Network.Fusion;
using R3;
using Unity.Profiling;
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
        // Profiler markers
        private static readonly ProfilerMarker s_enemyUpdateMarker = new("ProfilerMarker.Enemy.Update");
        private static readonly ProfilerMarker s_takeDamageMarker = new("ProfilerMarker.Enemy.TakeDamage");

        [Header("Components")]
        [SerializeField] private NavMeshAgent _navAgent;
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
        private float _attackRangeExitMultiplier;
        private int _itemDropGroupId;
        private int _expDropGroupId;

        // State
        private int _currentHp;
        private Transform _target;
        private bool _isDead;
        private int _networkId = -1;
        private IFusionRunnerService _runnerService;

        // Events
        private readonly Subject<SurvivorEnemyController> _onDeath = new();
        public Observable<SurvivorEnemyController> OnDeath => _onDeath;

        /// <summary>キルカウントに加算せず静かに回収する際のイベント</summary>
        private readonly Subject<SurvivorEnemyController> _onSilentRemoval = new();
        public Observable<SurvivorEnemyController> OnSilentRemoval => _onSilentRemoval;

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

        private bool _isPaused;
        public void SetPaused(bool paused) => _isPaused = paused;

        /// <summary>ネットワーク同期用ID（SurvivorEnemySpawnerが設定）</summary>
        public int NetworkId => _networkId;

        /// <summary>現在HP（ネットワーク同期用）</summary>
        public int CurrentHp => _currentHp;

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

        // R3 Observables — Presenter が購読
        private readonly Subject<Unit> _onHitReceived = new();
        public Observable<Unit> OnHitReceived => _onHitReceived;

        private readonly Subject<EnemyAnimationState> _onAnimationStateChanged = new();
        public Observable<EnemyAnimationState> OnAnimationStateChanged => _onAnimationStateChanged;

        // Presenter / Snapshot が読み取るプロパティ
        public EnemyAnimationState CurrentAnimationState { get; internal set; }
        public float NormalizedSpeed => _navAgent != null && _navAgent.speed > 0.01f
            ? _navAgent.velocity.magnitude / _navAgent.speed : 0f;

        /// <summary>NavMeshAgentの現在速度ベクトル（ネットワーク同期用）</summary>
        public Vector3 Velocity => _navAgent != null ? _navAgent.velocity : Vector3.zero;

        private void Awake()
        {
            if (_navAgent == null)
            {
                TryGetComponent(out _navAgent);
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
            IFusionRunnerService runnerService,
            float speedMultiplier = 1f,
            float healthMultiplier = 1f,
            float damageMultiplier = 1f,
            float experienceMultiplier = 1f,
            int itemDropGroupId = 0,
            int expDropGroupId = 0)
        {
            _runnerService = runnerService;
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
            _attackRangeExitMultiplier = master.AttackRangeExitMultiplier / 100f;

            _currentHp = _maxHp;
            _isDead = false;

            if (_navAgent != null)
            {
                _navAgent.speed = _moveSpeed;
                _navAgent.enabled = true;

                // NavMesh 上に明示的にスナップ（enabled 時の自動スナップが失敗するケースに対応）
                if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out var hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    _navAgent.Warp(hit.position);
                }
            }

            if (_collider != null)
            {
                _collider.enabled = true;
            }

            InitializeStateMachine();
        }

        private const float NavMeshCheckInterval = 1f;
        private const float UnreachableTimeout = 5f;
        private const float NavMeshSearchRadius = 50f;
        private float _navMeshCheckTimer;

        private float _unreachableTimer;

        private void Update()
        {
            if (_isPaused) return;

            using (s_enemyUpdateMarker.Auto())
            {
                _stateMachine?.Update();
            }

            // パス到達不能検知: NavAgent がパスを持てない状態が続く場合は強制デスポーン
            if (!_isDead && _navAgent != null && _navAgent.enabled && _target != null)
            {
                if (_navAgent.isOnNavMesh && !_navAgent.hasPath && !_navAgent.pathPending)
                {
                    _unreachableTimer += Time.deltaTime;
                    if (_unreachableTimer > UnreachableTimeout)
                    {
                        _unreachableTimer = 0f;
                        // キルカウントに加算させずに静かに回収（PerformDeath は使わない）
                        _isDead = true;
                        if (_navAgent != null) _navAgent.enabled = false;
                        if (_collider != null) _collider.enabled = false;
                        gameObject.SetActive(false);
                        _onSilentRemoval?.OnNext(this);
                        return;
                    }
                }
                else
                {
                    _unreachableTimer = 0f;
                }
            }

            // NavMesh から外れたエネミーを定期的に再スナップ（サーバー側のみ有効）
            if (_navAgent != null && _navAgent.enabled && !_isDead)
            {
                _navMeshCheckTimer -= Time.deltaTime;
                if (_navMeshCheckTimer <= 0f)
                {
                    _navMeshCheckTimer = NavMeshCheckInterval;
                    if (!_navAgent.isOnNavMesh)
                    {
                        // 広範囲で NavMesh を探索し、見つからなければ即座にデスポーン
                        if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out var hit, NavMeshSearchRadius, UnityEngine.AI.NavMesh.AllAreas))
                        {
                            _navAgent.Warp(hit.position);
                        }
                        else
                        {
                            Debug.LogWarning($"[SurvivorEnemyController] Enemy {_enemyId} (nid={_networkId}) unreachable from NavMesh, forcing death");
                            _currentHp = 0;
                            _hasPendingDamage = false;
                            PerformDeath();
                        }
                    }
                }
            }
        }

        public void TakeDamage(int damage)
        {
            using (s_takeDamageMarker.Auto())
            {
                TakeDamageWithStateMachine(damage);
            }
        }

        public void SetNetworkId(int id) => _networkId = id;

        /// <summary>
        /// Fusion Runner.DeltaTime を優先し、利用不可時は Time.deltaTime にフォールバック。
        /// ゲームロジックタイマー（攻撃クールダウン、HitStun）で使用。
        /// </summary>
        internal float GetDeltaTime()
        {
            if (_runnerService != null && _runnerService.IsActive && _runnerService.Runner != null)
                return _runnerService.Runner.DeltaTime;
            return Time.deltaTime;
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
            _networkId = -1;
            _hasPendingDamage = false;
            _pendingDamageAmount = 0;
            _target = null;
            // _stateMachine は再利用（遷移テーブルは不変のため InitializeStateMachine で再構築しない）
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
            _onHitReceived.Dispose();
            _onAnimationStateChanged.Dispose();
        }
    }
}
