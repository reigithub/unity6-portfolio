using Game.Core.Services;
using Game.Horror.Signals;
using Game.Library.Shared;
using Game.Shared.Combat;
using Game.Shared.Extensions;
using Game.Shared.Scriptable.Database.Tables;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Horror.Enemy
{
    /// <summary>
    /// ホラーゲームのゾンビ型敵 AI コントローラー。
    /// <para>
    /// 7状態 FSM（Dormant/Wander/Investigate/Chase/Attack/Stagger/Death）で
    /// 徘徊・知覚追跡・攻撃・被弾を制御する。
    /// NavMeshAgent の updatePosition=false/updateRotation=false + LateUpdate 手動同期により
    /// 震えを防ぐ。
    /// </para>
    /// </summary>
    // [RequireComponent(typeof(NavMeshAgent))]
    // [RequireComponent(typeof(Animator))]
    public partial class HorrorEnemyController : MonoBehaviour, IDamageable
    {
        [SerializeField] private NavMeshAgent _navMeshAgent;
        [SerializeField] private Animator _animator;
        [SerializeField] private HorrorEnemyPerception _perception;

        [Tooltip("true の場合、初期ステートを Dormant にする（配置済み敵の遅延起動用）")]
        [SerializeField] private bool _startDormant;

        private StateMachine<HorrorEnemyController, StateEvent> _stateMachine;
        private bool _initialized;

        // Initialize で注入されるデータ
        private GameObject _player;
        private HorrorEnemyMaster _master;
        private int _spawnId;
        private IDamageable _playerDamageable;
        private IMessagePipeService _messagePipeService;

        // 体力・速度
        private int _health;
        private float _currentTargetSpeed;
        private float _currentAnimSpeed; // Animator に渡す平滑化済みの Speed 値

        // 目的地更新の間引き管理（震え防止）
        private Vector3 _lastDestination;
        private float _repathTimer;

        // アニメーターパラメータハッシュ（StringToHash でキャッシュ）
        private readonly int _animHashSpeed = Animator.StringToHash("Speed");
        private readonly int _animHashAttack = Animator.StringToHash("Attack");
        private readonly int _animHashStagger = Animator.StringToHash("Stagger");
        private readonly int _animHashDeath = Animator.StringToHash("Death");

        // 定数
        private const float WanderRadius = 8f;
        private const float ScreamLoudness = 2f;
        private const float VelocityThreshold = 0.001f;
        private const float RotationSmoothSpeed = 10f;
        private const float AnimSpeedResponse = 8f; // アニメーター Speed 補間の応答速度（大きいほど速く追従）

        /// <summary>
        /// コントローラーを初期化する。スポーナーまたはシーン初期化から呼ぶ。
        /// </summary>
        /// <param name="player">プレイヤーの GameObject</param>
        /// <param name="master">調整値マスターデータ</param>
        /// <param name="spawnId">スポーンエントリの一意 Id（HorrorEnemySpawnMaster の Id）。撃破記録の永続化キー</param>
        public void Initialize(GameObject player, HorrorEnemyMaster master, int spawnId)
        {
            _player = player;
            _master = master;
            _spawnId = spawnId;

            if (player.TryGetComponent<IDamageable>(out var damageable))
                _playerDamageable = damageable;

            // TryGetComponent(out _navMeshAgent);
            // TryGetComponent(out _animator);

            // NavMeshAgent の自動位置・回転更新を無効化（震え防止）
            // LateUpdate で手動同期する
            if (_navMeshAgent != null)
            {
                _navMeshAgent.updatePosition = false;
                _navMeshAgent.updateRotation = false;
            }

            _health = master.MaxHealth;

            // 知覚センサーを初期化（視覚/聴覚の購読含む）
            _perception.Initialize(player.transform, master);

            // MessagePipe サービスをキャッシュ（Scream / Enemy.Died 発火用）
            _messagePipeService = GameServiceManager.Resolve<IMessagePipeService>();

            InitializeStateMachine();
            _initialized = true;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Log($"[HorrorEnemyController] Initialize: player={player.name}, health={_health}, dormant={_startDormant}");
#endif
        }

        #region MonoBehaviour

        private void Update()
        {
            if (!_initialized) return;
            EnsureDeadState();
            _stateMachine.Update();
        }

        private void FixedUpdate()
        {
            if (!_initialized) return;
            _stateMachine.FixedUpdate();
        }

        private void LateUpdate()
        {
            if (!_initialized) return;
            SyncPositionAndRotation();
            _stateMachine.LateUpdate();
        }

        #endregion

        #region IDamageable

        /// <summary>死亡フラグ（体力が 0 以下）</summary>
        public bool IsDead => _health <= 0;

        public void TakeDamage(int damage)
        {
            if (IsDead) return;

            _health -= damage;

            if (_health <= 0)
            {
                _health = 0;

                // DeathState への遷移は Update の EnsureDeadState が宣言的に保証する
                // 未初期化時に撃破記録を無音で失わないよう、あえて ?. を使わない
                _messagePipeService.Publish(new HorrorSignals.Enemy.Died(_spawnId));
            }
            else
            {
                if (_stateMachine != null && _stateMachine.IsProcessing())
                    _stateMachine.Transition(StateEvent.Stagger);
            }
        }

        #endregion

        #region Position Sync（Anti-Jitter）

        /// <summary>
        /// NavMeshAgent の計算位置・速度を Transform にスムーズに同期する。
        /// updatePosition=false なので LateUpdate で手動呼び出しが必要。
        /// </summary>
        private void SyncPositionAndRotation()
        {
            if (_navMeshAgent == null) return;

            // NavMeshAgent の計算位置を Transform に反映
            transform.position = _navMeshAgent.nextPosition;

            // 移動方向へ Slerp で回転（移動中のみ）
            if (_navMeshAgent.velocity.sqrMagnitude > VelocityThreshold)
            {
                var targetRotation = Quaternion.LookRotation(_navMeshAgent.velocity.normalized);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    RotationSmoothSpeed * Time.deltaTime);
            }

            // アニメーター Speed 更新（目標値へ指数補間で追従させ BlendTree の急変を防ぐ。停止時の 0 落下も同経路で滑らかになる）
            bool isMoving = _navMeshAgent.velocity.sqrMagnitude > VelocityThreshold;
            float targetAnimSpeed = isMoving ? _currentTargetSpeed : 0f;
            _currentAnimSpeed = CalculateSmoothedAnimSpeed(_currentAnimSpeed, targetAnimSpeed, AnimSpeedResponse, Time.deltaTime);
            if (_animator) _animator.SetFloat(_animHashSpeed, _currentAnimSpeed);
        }

        #endregion

        #region Speed Control

        /// <summary>
        /// NavMeshAgent の速度とアニメーション用速度を設定する。
        /// </summary>
        /// <param name="speed">移動速度</param>
        private void SetSpeed(float speed)
        {
            if (_navMeshAgent != null) _navMeshAgent.speed = speed;
            _currentTargetSpeed = speed;
        }

        /// <summary>アニメーター Speed の平滑化値を算出する（指数補間・フレームレート非依存）。</summary>
        internal static float CalculateSmoothedAnimSpeed(float current, float target, float response, float deltaTime)
            => Mathf.Lerp(current, target, 1f - Mathf.Exp(-response * deltaTime));

        #endregion

        #region Navigation

        /// <summary>
        /// RepathInterval で間引きながら目的地を更新する。
        /// 大きく目的地がずれた場合は即座に更新する。
        /// SetDestinationImmediate を使用（Unity 6 の SetDestination バグ回避）。
        /// </summary>
        /// <param name="target">目的地のワールド座標</param>
        private void MoveToThrottled(Vector3 target)
        {
            if (_navMeshAgent == null || _master == null) return;

            float distFromLast = Vector3.Distance(target, _lastDestination);
            if (distFromLast > 1f)
            {
                float leniency = _navMeshAgent.radius + _navMeshAgent.stoppingDistance + _navMeshAgent.height;
                if (_navMeshAgent.SetDestinationImmediate(target, leniency))
                    _lastDestination = target;
                _repathTimer = 0f;
                return;
            }

            _repathTimer += Time.deltaTime;
            if (_repathTimer >= _master.RepathInterval)
            {
                _repathTimer = 0f;
                float leniency = _navMeshAgent.radius + _navMeshAgent.stoppingDistance + _navMeshAgent.height;
                if (_navMeshAgent.SetDestinationImmediate(target, leniency))
                    _lastDestination = target;
            }
        }

        /// <summary>
        /// ランダムな NavMesh 上の点へ向かう。
        /// 到着済みのときのみ次の目的地を設定する。
        /// </summary>
        private void WanderToRandomPoint()
        {
            if (_navMeshAgent == null) return;
            if (_navMeshAgent.pathPending) return;
            if (_navMeshAgent.remainingDistance > 0.5f) return;

            var randomDir = Random.insideUnitSphere * WanderRadius;
            randomDir.y = 0f;
            var randomPos = transform.position + randomDir;

            if (NavMesh.SamplePosition(randomPos, out var hit, WanderRadius, NavMesh.AllAreas))
            {
                float leniency = _navMeshAgent.radius + _navMeshAgent.stoppingDistance + _navMeshAgent.height;
                if (_navMeshAgent.SetDestinationImmediate(hit.position, leniency))
                    _lastDestination = hit.position;
            }
        }

        /// <summary>
        /// プレイヤーの方向へ緩やかに回転する（AttackState の正面向き用）。
        /// </summary>
        private void FaceTarget()
        {
            if (!_player) return;
            var forward = _player.transform.position - transform.position;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) return;
            var lookRot = Quaternion.LookRotation(forward);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, RotationSmoothSpeed * Time.deltaTime);
        }

        /// <summary>
        /// プレイヤーまでの距離を返す。プレイヤーが null の場合は float.MaxValue。
        /// </summary>
        private float DistanceToPlayer()
        {
            if (!_player) return float.MaxValue;
            return Vector3.Distance(transform.position, _player.transform.position);
        }

        /// <summary>
        /// 現在のプレイヤー位置が攻撃間合い内かどうかを返す。
        /// </summary>
        private bool IsWithinAttackRange()
        {
            return _master != null && DistanceToPlayer() <= _master.AttackRange;
        }

        /// <summary>NavMeshAgent を停止する。</summary>
        private void StopAgent()
        {
            if (_navMeshAgent != null && _navMeshAgent.isOnNavMesh) _navMeshAgent.isStopped = true;
        }

        /// <summary>NavMeshAgent を再開する。</summary>
        private void ResumeAgent()
        {
            if (_navMeshAgent != null && _navMeshAgent.isOnNavMesh) _navMeshAgent.isStopped = false;
        }

        #endregion

        #region Combat / Sound

        /// <summary>
        /// ホード伝播用のスクリーム HorrorSignals.Noise.Occurred を MessagePipe で Publish する。
        /// ChaseState への突入時に呼ぶ。
        /// </summary>
        private void PublishScream()
        {
            _messagePipeService?.Publish(new HorrorSignals.Noise.Occurred(transform.position, ScreamLoudness, NoiseType.Scream));
        }

        /// <summary>
        /// プレイヤーへ攻撃ダメージを与える。
        /// </summary>
        private void ApplyAttackDamage()
        {
            _playerDamageable?.TakeDamage(_master.AttackDamage);
        }

        #endregion

        #region Animator Helpers

        private void TriggerAttack()
        {
            if (_animator != null) _animator.SetTrigger(_animHashAttack);
        }

        private void TriggerStagger()
        {
            if (_animator != null) _animator.SetTrigger(_animHashStagger);
        }

        private void TriggerDeath()
        {
            if (_animator != null) _animator.SetTrigger(_animHashDeath);
        }

        #endregion
    }
}
