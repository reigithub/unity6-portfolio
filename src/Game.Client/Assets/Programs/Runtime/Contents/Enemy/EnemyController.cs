using Game.Core;
using Game.Core.Constants;
using Game.Core.MasterData.MemoryTables;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Contents.Enemy
{
    /// <summary>
    /// 簡易的なエネミー追尾システム
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Animator))]
    public class EnemyController : MonoBehaviour
    {
        [SerializeField] private GameObject _player;

        private NavMeshAgent _navMeshAgent;
        private Animator _animator;

        // ステートマシーン
        private StateMachine<EnemyController, StateEvent> _stateMachine;

        // 検知関連
        private readonly RaycastHit[] _raycastHits = new RaycastHit[1];
        private readonly Collider[] _overlapResults = new Collider[10];

        // パトロール関連
        private readonly float _rotationSpeed = 5.0f;
        private float _rotationInterval = 5.0f;
        private float _rotationIntervalCount;
        private float _remainingDistance = 0.5f;

        // アニメータハッシュ
        private readonly int _animatorHashSpeed = Animator.StringToHash("Speed");

        public EnemyMaster EnemyMaster { get; private set; }

        public void Initialize(GameObject player, EnemyMaster enemyMaster)
        {
            _player = player;
            EnemyMaster = enemyMaster;

            TryGetComponent(out _navMeshAgent);
            TryGetComponent(out _animator);

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

        #endregion

        #region Speed Control

        private void SetSpeed(float speed)
        {
            if (_navMeshAgent) _navMeshAgent.speed = speed;
            if (_animator) _animator.SetFloat(_animatorHashSpeed, speed);
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
            var viewAngle = Vector3.Angle(transform.forward, viewDistance) * (viewCross.y < 0f ? -1f : 1f) + 180f;
            if (viewAngle <= 45f || viewAngle >= 315f)
            {
                Vector3 distance = _player.transform.position - transform.position;
                float maxDistance = distance.magnitude;
                Vector3 direction = distance.normalized;
                Vector3 eyePosition = transform.position + new Vector3(0f, 0.5f, 0f);

                // 視線が遮られていないかチェック
                var raycastHitCount = Physics.RaycastNonAlloc(new Ray(eyePosition, direction), _raycastHits, maxDistance, LayerMaskConstants.PlayerLayerMask);
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
            float radius = distance * 2f;
            var hitCount = Physics.OverlapSphereNonAlloc(transform.position, radius, _overlapResults, LayerMaskConstants.PlayerLayerMask);
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

                _navMeshAgent.SetDestination(position);
                return true;
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
            _rotationInterval = Random.Range(3f, 8f);
            _remainingDistance = Random.Range(0.3f, 0.8f);
        }

        #endregion

        #region StateMachine

        private void InitializeStateMachine()
        {
            _stateMachine = new StateMachine<EnemyController, StateEvent>(this);

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
        private class PatrolState : State<EnemyController, StateEvent>
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

                var randomPos = ctx.transform.position + new Vector3(Random.Range(-10f, 10f), 0f, Random.Range(-10f, 10f));
                ctx.TrySetDestination(randomPos, remainingDistance: ctx._remainingDistance);

                if (ctx._rotationIntervalCount > ctx._rotationInterval)
                {
                    var forward = new Vector3(0f, Random.Range(0f, 180f), 0f);
                    var lookRotation = Quaternion.LookRotation(forward);
                    var slerp = Quaternion.Slerp(ctx.transform.rotation, lookRotation, 10f * Time.deltaTime);
                    ctx.transform.rotation = slerp;
                    ctx.ResetPatrolRotation();
                }
            }
        }

        /// <summary>
        /// 追跡状態: 視覚でプレイヤーを検知、走って追跡
        /// </summary>
        private class ChaseState : State<EnemyController, StateEvent>
        {
            public override void Enter()
            {
                var ctx = Context;
                ctx.SetSpeed(ctx.EnemyMaster.RunSpeed);
            }

            public override void Update()
            {
                // 視覚検知継続チェック
                var ctx = Context;
                if (ctx.TryDetectPlayerByVision())
                {
                    // プレイヤーの位置に向かう
                    if (NavMesh.SamplePosition(ctx._player.transform.position, out var navMeshHit, 1f, 1))
                    {
                        ctx.TrySetDestination(navMeshHit.position, ignoreDistance: true);
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
        }

        /// <summary>
        /// 捜索状態: 聴覚でプレイヤーを検知、歩いて捜索
        /// </summary>
        private class SearchState : State<EnemyController, StateEvent>
        {
            public override void Enter()
            {
                var ctx = Context;
                ctx.SetSpeed(ctx.EnemyMaster.WalkSpeed);
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

                    // プレイヤーの近くのランダムな位置に向かう
                    var distance = ctx.EnemyMaster.AuditoryDistance;
                    float x = ctx._player.transform.position.x + Random.Range(-distance, distance);
                    float z = ctx._player.transform.position.z + Random.Range(-distance, distance);

                    if (NavMesh.SamplePosition(new Vector3(x, 0f, z), out var navMeshHit, 1f, 1))
                    {
                        ctx.TrySetDestination(navMeshHit.position, ignoreDistance: true);
                    }

                    return;
                }

                // 完全に見失った
                StateMachine.Transition(StateEvent.LostPlayer);
            }
        }

        #endregion
    }
}