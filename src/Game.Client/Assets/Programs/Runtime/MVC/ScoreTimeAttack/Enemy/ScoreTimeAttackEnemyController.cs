using Game.Shared;
using Game.Shared.Constants;
using Game.Shared.Extensions;
using Game.Client.MasterData;
using UnityEngine;
using UnityEngine.AI;

namespace Game.ScoreTimeAttack.Enemy
{
    /// <summary>
    /// 簡易的なエネミー追尾システム
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Animator))]
    public class ScoreTimeAttackEnemyController : MonoBehaviour
    {
        [SerializeField] private GameObject _player;

        private NavMeshAgent _navMeshAgent;
        private Animator _animator;

        // ステートマシーン
        private StateMachine<ScoreTimeAttackEnemyController, StateEvent> _stateMachine;

        // 検知関連
        private readonly RaycastHit[] _raycastHits = new RaycastHit[1];
        private readonly Collider[] _overlapResults = new Collider[10];

        // 目的地更新の最適化（震え防止）
        private Vector3 _lastDestination;
        private float _destinationUpdateTimer;

        // パトロール関連
        private readonly float _rotationSpeed = 5.0f;
        private float _rotationInterval = 5.0f;
        private float _rotationIntervalCount;
        private float _remainingDistance = EnemyConstants.Patrol.DefaultRemainingDistance;

        // アニメータハッシュ
        private readonly int _animatorHashSpeed = Animator.StringToHash("Speed");

        // 現在の目標速度（SetSpeedで設定される）
        private float _currentTargetSpeed;

        public ScoreTimeAttackEnemyMaster EnemyMaster { get; private set; }

        public void Initialize(GameObject player, ScoreTimeAttackEnemyMaster enemyMaster)
        {
            _player = player;
            EnemyMaster = enemyMaster;

            TryGetComponent(out _navMeshAgent);
            TryGetComponent(out _animator);

            // NavMeshAgentの自動位置・回転更新を無効化（震え防止）
            // LateUpdateで手動でスムーズに同期する
            if (_navMeshAgent)
            {
                _navMeshAgent.updatePosition = false;
                _navMeshAgent.updateRotation = false;
            }

            SetSpeed(enemyMaster.WalkSpeed);

            // ステートマシン初期化
            InitializeStateMachine();
        }

        #region MonoBehaviour Methods

        private void Update()
        {
            if (!_player) return;

            _stateMachine.Update();
        }

        private void LateUpdate()
        {
            if (!_player) return;

            _stateMachine?.LateUpdate();
        }

        #endregion

        #region Position Sync (Anti-Jitter)

        /// <summary>
        /// NavMeshAgentの位置・回転をTransformにスムーズに同期
        /// updatePosition=falseなので手動で同期する必要がある
        /// </summary>
        private void SyncPositionAndRotation()
        {
            if (!_navMeshAgent) return;

            // NavMeshAgentの計算位置をTransformに反映
            transform.position = _navMeshAgent.nextPosition;

            // 回転の同期（移動方向を向く）
            if (_navMeshAgent.velocity.sqrMagnitude > EnemyConstants.Navigation.VelocityThreshold)
            {
                var targetRotation = Quaternion.LookRotation(_navMeshAgent.velocity.normalized);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    EnemyConstants.Navigation.RotationSmoothSpeed * Time.deltaTime
                );
            }

            // アニメーション更新
            UpdateAnimation();
        }

        #endregion

        #region Speed Control

        /// <summary>
        /// NavMeshAgentの移動速度とアニメーション用の目標速度を設定
        /// </summary>
        private void SetSpeed(float speed)
        {
            if (_navMeshAgent) _navMeshAgent.speed = speed;
            _currentTargetSpeed = speed;
        }

        /// <summary>
        /// アニメーションを更新
        /// - 移動中: SetSpeedで設定した目標速度をアニメーターに設定
        /// - 停止中: 0を設定（Idle）
        /// Speed閾値: Idle<=0, Walk>1, Run>4
        /// </summary>
        private void UpdateAnimation()
        {
            if (!_animator || !_navMeshAgent) return;

            // NavMeshAgentが実際に移動しているかを判定
            bool isMoving = _navMeshAgent.velocity.sqrMagnitude > EnemyConstants.Navigation.VelocityThreshold;

            // 移動中なら目標速度、停止中なら0
            float animSpeed = isMoving ? _currentTargetSpeed : 0f;
            _animator.SetFloat(_animatorHashSpeed, animSpeed);
        }

        #endregion

        #region Detection

        private bool TryDetectPlayerByVision()
        {
            // 視覚範囲内のプレイヤーを検知
            if (!IsPlayerOverlap(EnemyMaster.VisualDistance))
                return false;

            // 視野角チェック
            Vector3 viewDistance = transform.position - _player.transform.position;
            Vector3 viewCross = Vector3.Cross(transform.forward, viewDistance);
            var viewAngle = Vector3.Angle(transform.forward, viewDistance) * (viewCross.y < 0f ? -1f : 1f) + EnemyConstants.Vision.AngleOffset;
            if (viewAngle <= EnemyConstants.Vision.ForwardAngleMin || viewAngle >= EnemyConstants.Vision.ForwardAngleMax)
            {
                Vector3 distance = _player.transform.position - transform.position;
                float maxDistance = distance.magnitude;
                Vector3 direction = distance.normalized;
                Vector3 eyePosition = transform.position + new Vector3(0f, EnemyConstants.Vision.EyeHeightOffset, 0f);

                // 視線が遮られていないかチェック
                var raycastHitCount = Physics.RaycastNonAlloc(new Ray(eyePosition, direction), _raycastHits, maxDistance, LayerMaskConstants.Player);
                if (raycastHitCount > 0 && _raycastHits[0].transform.gameObject == _player)
                    return true;
            }

            return false;
        }

        private bool TryDetectPlayerByAudio()
        {
            // 聴覚範囲内のプレイヤーを検知
            return IsPlayerOverlap(EnemyMaster.AuditoryDistance);
        }

        private bool IsPlayerOverlap(float distance)
        {
            float radius = distance * EnemyConstants.Detection.RadiusMultiplier;
            var hitCount = Physics.OverlapSphereNonAlloc(transform.position, radius, _overlapResults, LayerMaskConstants.Player);
            if (hitCount == 0)
                return false;

            // プレイヤーが範囲内にいるか確認
            for (int i = 0; i < hitCount; i++)
            {
                if (_overlapResults[i].gameObject == _player)
                    return true;
            }

            return false;
        }

        #endregion

        #region Navigation

        private bool TrySetDestination(Vector3 position, bool ignoreDistance = false, float remainingDistance = 0.5f)
        {
            if (!_navMeshAgent) return false;

            if (_navMeshAgent.pathStatus != NavMeshPathStatus.PathInvalid)
            {
                if (!ignoreDistance && _navMeshAgent.remainingDistance > remainingDistance)
                    return false;

                // Unity 6 バグ回避: SetDestinationImmediateを使用
                // positionLeniency = radius + stoppingDistance + height
                float leniency = _navMeshAgent.radius + _navMeshAgent.stoppingDistance + _navMeshAgent.height;
                if (_navMeshAgent.SetDestinationImmediate(position, leniency))
                {
                    _lastDestination = position;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 目的地更新を最適化（毎フレーム更新による震えを防止）
        /// - 一定間隔でのみ更新
        /// - プレイヤーが大きく移動した場合は即座に更新
        /// </summary>
        private bool TrySetDestinationThrottled(Vector3 position)
        {
            if (!_navMeshAgent) return false;

            // プレイヤーが大きく移動した場合は即座に更新
            float distanceFromLastDest = Vector3.Distance(position, _lastDestination);
            if (distanceFromLastDest > EnemyConstants.Navigation.DestinationUpdateThreshold)
            {
                return TrySetDestination(position, ignoreDistance: true);
            }

            // 一定間隔でのみ更新
            _destinationUpdateTimer += Time.deltaTime;
            if (_destinationUpdateTimer >= EnemyConstants.Navigation.DestinationUpdateInterval)
            {
                _destinationUpdateTimer = 0f;
                return TrySetDestination(position, ignoreDistance: true);
            }

            return false;
        }

        private void LookAtPlayer(float rotationSpeed)
        {
            var forward = _player.transform.position - transform.position;
            forward.y = 0f;

            var lookRotation = Quaternion.LookRotation(forward);
            var slerp = Quaternion.Slerp(transform.rotation, lookRotation, rotationSpeed * Time.deltaTime);
            transform.rotation = slerp;
        }

        private void ResetPatrolRotation()
        {
            _rotationIntervalCount = 0f;
            _rotationInterval = Random.Range(EnemyConstants.Patrol.RotationIntervalMin, EnemyConstants.Patrol.RotationIntervalMax);
            _remainingDistance = Random.Range(EnemyConstants.Patrol.RemainingDistanceMin, EnemyConstants.Patrol.RemainingDistanceMax);
        }

        #endregion

        #region StateMachine

        private void InitializeStateMachine()
        {
            _stateMachine = new StateMachine<ScoreTimeAttackEnemyController, StateEvent>(this);

            // 状態遷移テーブルの構築
            // Patrol → Chase/Search
            _stateMachine.AddTransition<PatrolState, ChaseState>(StateEvent.DetectByVision);
            _stateMachine.AddTransition<PatrolState, SearchState>(StateEvent.DetectByAudio);

            // Chase → Patrol/Search
            _stateMachine.AddTransition<ChaseState, PatrolState>(StateEvent.LostPlayer);
            _stateMachine.AddTransition<ChaseState, SearchState>(StateEvent.DetectByAudio);

            // Search → Patrol/Chase
            _stateMachine.AddTransition<SearchState, PatrolState>(StateEvent.LostPlayer);
            _stateMachine.AddTransition<SearchState, ChaseState>(StateEvent.DetectByVision);

            // 何もなければ初期ステートに戻る
            _stateMachine.AddTransition<PatrolState>(StateEvent.LostPlayer);

            // 初期ステート
            _stateMachine.SetInitState<PatrolState>();
        }

        /// <summary>
        /// 状態遷移イベントKey
        /// </summary>
        private enum StateEvent
        {
            DetectByVision, // 視覚で検知: → Chase
            DetectByAudio,  // 聴覚で検知: → Search
            LostPlayer,     // プレイヤーを見失う: → Patrol
        }

        /// <summary>
        /// パトロール状態: プレイヤー未検知時のランダム巡回
        /// </summary>
        private class PatrolState : State<ScoreTimeAttackEnemyController, StateEvent>
        {
            public override void Enter()
            {
                var ctx = Context;
                ctx.SetSpeed(ctx.EnemyMaster.WalkSpeed);
                ctx.ResetPatrolRotation();
            }

            public override void Update()
            {
                // 視覚検知チェック
                var ctx = Context;
                if (ctx.TryDetectPlayerByVision())
                {
                    StateMachine.Transition(StateEvent.DetectByVision);
                    return;
                }

                // 聴覚検知チェック
                if (ctx.TryDetectPlayerByAudio())
                {
                    StateMachine.Transition(StateEvent.DetectByAudio);
                    return;
                }

                // ランダムパトロール
                ctx._rotationIntervalCount += Time.deltaTime;

                var randomPos = ctx.transform.position + new Vector3(
                    Random.Range(-EnemyConstants.Patrol.RandomMoveRange, EnemyConstants.Patrol.RandomMoveRange),
                    0f,
                    Random.Range(-EnemyConstants.Patrol.RandomMoveRange, EnemyConstants.Patrol.RandomMoveRange));
                ctx.TrySetDestination(randomPos, remainingDistance: ctx._remainingDistance);

                if (ctx._rotationIntervalCount > ctx._rotationInterval)
                {
                    var forward = new Vector3(0f, Random.Range(0f, EnemyConstants.Patrol.RandomRotationAngleMax), 0f);
                    var lookRotation = Quaternion.LookRotation(forward);
                    var slerp = Quaternion.Slerp(ctx.transform.rotation, lookRotation, EnemyConstants.Patrol.PatrolRotationSpeed * Time.deltaTime);
                    ctx.transform.rotation = slerp;
                    ctx.ResetPatrolRotation();
                }
            }

            public override void LateUpdate()
            {
                Context.SyncPositionAndRotation();
            }
        }

        /// <summary>
        /// 追跡状態: 視覚でプレイヤーを検知、走って追跡
        /// </summary>
        private class ChaseState : State<ScoreTimeAttackEnemyController, StateEvent>
        {
            public override void Enter()
            {
                var ctx = Context;
                ctx.SetSpeed(ctx.EnemyMaster.RunSpeed);
                ctx._destinationUpdateTimer = EnemyConstants.Navigation.DestinationUpdateInterval; // 即座に目的地を設定
            }

            public override void Update()
            {
                // 視覚検知継続チェック
                var ctx = Context;
                if (ctx.TryDetectPlayerByVision())
                {
                    // プレイヤーの位置に向かう（最適化版：毎フレーム更新しない）
                    if (NavMesh.SamplePosition(ctx._player.transform.position, out var navMeshHit, 1f, 1))
                    {
                        ctx.TrySetDestinationThrottled(navMeshHit.position);
                    }

                    return;
                }

                // 視覚で見失った場合、聴覚検知チェック
                if (ctx.TryDetectPlayerByAudio())
                {
                    StateMachine.Transition(StateEvent.DetectByAudio);
                    return;
                }

                // 完全に見失った
                StateMachine.Transition(StateEvent.LostPlayer);
            }

            public override void LateUpdate()
            {
                Context.SyncPositionAndRotation();
            }
        }

        /// <summary>
        /// 捜索状態: 聴覚でプレイヤーを検知、歩いて捜索
        /// </summary>
        private class SearchState : State<ScoreTimeAttackEnemyController, StateEvent>
        {
            public override void Enter()
            {
                var ctx = Context;
                ctx.SetSpeed(ctx.EnemyMaster.WalkSpeed);
                ctx._destinationUpdateTimer = EnemyConstants.Navigation.DestinationUpdateInterval; // 即座に目的地を設定
            }

            public override void Update()
            {
                // 視覚検知チェック（優先度高）
                var ctx = Context;
                if (ctx.TryDetectPlayerByVision())
                {
                    StateMachine.Transition(StateEvent.DetectByVision);
                    return;
                }

                // 聴覚検知継続チェック
                if (ctx.TryDetectPlayerByAudio())
                {
                    // プレイヤーの方を向く
                    ctx.LookAtPlayer(ctx._rotationSpeed);

                    // プレイヤーの近くのランダムな位置に向かう（最適化版）
                    var distance = ctx.EnemyMaster.AuditoryDistance;
                    float x = ctx._player.transform.position.x + Random.Range(-distance, distance);
                    float z = ctx._player.transform.position.z + Random.Range(-distance, distance);

                    if (NavMesh.SamplePosition(new Vector3(x, 0f, z), out var navMeshHit, 1f, 1))
                    {
                        ctx.TrySetDestinationThrottled(navMeshHit.position);
                    }

                    return;
                }

                // 完全に見失った
                StateMachine.Transition(StateEvent.LostPlayer);
            }

            public override void LateUpdate()
            {
                Context.SyncPositionAndRotation();
            }
        }

        #endregion
    }
}