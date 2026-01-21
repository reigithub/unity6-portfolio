using Game.Shared;
using UnityEngine;

namespace Game.MVP.Survivor.Enemy
{
    /// <summary>
    /// SurvivorEnemyController - StateMachine実装部分
    /// </summary>
    public partial class SurvivorEnemyController
    {
        // Combat Settings（値はInitialize()でマスターデータから設定）
        // _attackRange, _attackCooldown, _hitStunDuration, _rotationSpeed は本体クラスで定義

        // Constants
        private const float AttackRangeExitMultiplier = 1.2f;

        // Timers
        private float _attackTimer;
        private float _hitStunTimer;

        // Event Flags (State内部からのみ参照)
        private bool _hasPendingDamage;
        private int _pendingDamageAmount;

        // StateMachine
        private StateMachine<SurvivorEnemyController, EnemyEvent> _stateMachine;

        // Animator hash for Attack
        private static readonly int AttackHash = Animator.StringToHash("Attack");

        private void InitializeStateMachine()
        {
            _stateMachine = new StateMachine<SurvivorEnemyController, EnemyEvent>(this);

            // 遷移テーブル構築
            _stateMachine.AddTransition<IdleState, ChaseState>(EnemyEvent.FoundTarget);
            _stateMachine.AddTransition<ChaseState, AttackState>(EnemyEvent.EnterAttackRange);
            _stateMachine.AddTransition<ChaseState, IdleState>(EnemyEvent.LostTarget);
            _stateMachine.AddTransition<AttackState, ChaseState>(EnemyEvent.ExitAttackRange);
            _stateMachine.AddTransition<AttackState, IdleState>(EnemyEvent.LostTarget);
            _stateMachine.AddTransition<ChaseState, HitStunState>(EnemyEvent.TakeHit);
            _stateMachine.AddTransition<AttackState, HitStunState>(EnemyEvent.TakeHit);
            _stateMachine.AddTransition<IdleState, HitStunState>(EnemyEvent.TakeHit);
            _stateMachine.AddTransition<HitStunState, ChaseState>(EnemyEvent.RecoverFromHit);
            _stateMachine.AddTransition<IdleState, DeathState>(EnemyEvent.Die);
            _stateMachine.AddTransition<ChaseState, DeathState>(EnemyEvent.Die);
            _stateMachine.AddTransition<AttackState, DeathState>(EnemyEvent.Die);
            _stateMachine.AddTransition<HitStunState, DeathState>(EnemyEvent.Die);

            // 初期ステート
            _stateMachine.SetInitState<ChaseState>();
        }

        /// <summary>
        /// 状態遷移イベント
        /// </summary>
        private enum EnemyEvent
        {
            FoundTarget,
            LostTarget,
            EnterAttackRange,
            ExitAttackRange,
            TakeHit,
            RecoverFromHit,
            Die
        }

        /// <summary>
        /// ダメージリクエストを設定（外部から呼び出し、State内で処理）
        /// </summary>
        private void RequestDamage(int damage)
        {
            _hasPendingDamage = true;
            _pendingDamageAmount = damage;
        }

        /// <summary>
        /// ダメージイベントを処理（State内から呼び出し）
        /// </summary>
        private bool TryProcessDamage(out bool shouldDie)
        {
            shouldDie = false;
            if (!_hasPendingDamage) return false;

            _hasPendingDamage = false;
            _currentHp -= _pendingDamageAmount;
            _hitStunTimer = _hitStunDuration;

            if (_animator != null)
            {
                _animator.SetTrigger(HitHash);
            }

            shouldDie = _currentHp <= 0;
            return true;
        }

        /// <summary>
        /// 基底State: 共通のダメージ/死亡チェック
        /// </summary>
        private abstract class EnemyStateBase : State<SurvivorEnemyController, EnemyEvent>
        {
            /// <summary>
            /// ダメージチェックと遷移処理
            /// </summary>
            /// <returns>遷移が発生した場合true</returns>
            protected bool CheckDamageAndTransition()
            {
                var ctx = Context;
                if (ctx.TryProcessDamage(out bool shouldDie))
                {
                    if (shouldDie)
                    {
                        StateMachine.Transition(EnemyEvent.Die);
                    }
                    else
                    {
                        StateMachine.Transition(EnemyEvent.TakeHit);
                    }
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 待機状態
        /// </summary>
        private class IdleState : EnemyStateBase
        {
            public override void Enter()
            {
                var ctx = Context;
                if (ctx._navAgent != null && ctx._navAgent.isOnNavMesh)
                {
                    ctx._navAgent.isStopped = true;
                }

                if (ctx._animator != null)
                {
                    ctx._animator.SetFloat(SpeedHash, 0f);
                }
            }

            public override void Update()
            {
                if (CheckDamageAndTransition()) return;

                var ctx = Context;
                if (ctx._target != null)
                {
                    StateMachine.Transition(EnemyEvent.FoundTarget);
                }
            }
        }

        /// <summary>
        /// 追跡状態
        /// </summary>
        private class ChaseState : EnemyStateBase
        {
            public override void Enter()
            {
                var ctx = Context;
                if (ctx._navAgent != null && ctx._navAgent.isOnNavMesh)
                {
                    ctx._navAgent.isStopped = false;
                }
            }

            public override void Update()
            {
                if (CheckDamageAndTransition()) return;

                var ctx = Context;

                if (ctx._target == null)
                {
                    StateMachine.Transition(EnemyEvent.LostTarget);
                    return;
                }

                float distance = Vector3.Distance(ctx.transform.position, ctx._target.position);
                if (distance <= ctx._attackRange)
                {
                    StateMachine.Transition(EnemyEvent.EnterAttackRange);
                    return;
                }

                if (ctx._navAgent != null && ctx._navAgent.isOnNavMesh)
                {
                    ctx._navAgent.SetDestination(ctx._target.position);

                    if (ctx._animator != null)
                    {
                        float speed = ctx._navAgent.velocity.magnitude / Mathf.Max(ctx._navAgent.speed, 0.01f);
                        ctx._animator.SetFloat(SpeedHash, speed);
                    }
                }
            }
        }

        /// <summary>
        /// 攻撃状態
        /// </summary>
        private class AttackState : EnemyStateBase
        {
            public override void Enter()
            {
                var ctx = Context;
                if (ctx._navAgent != null && ctx._navAgent.isOnNavMesh)
                {
                    ctx._navAgent.isStopped = true;
                }

                if (ctx._animator != null)
                {
                    ctx._animator.SetFloat(SpeedHash, 0f);
                }
            }

            public override void Update()
            {
                if (CheckDamageAndTransition()) return;

                var ctx = Context;

                if (ctx._target == null)
                {
                    StateMachine.Transition(EnemyEvent.LostTarget);
                    return;
                }

                float distance = Vector3.Distance(ctx.transform.position, ctx._target.position);
                if (distance > ctx._attackRange * AttackRangeExitMultiplier)
                {
                    StateMachine.Transition(EnemyEvent.ExitAttackRange);
                    return;
                }

                // プレイヤーの方を向く
                Vector3 direction = (ctx._target.position - ctx.transform.position).normalized;
                direction.y = 0;
                if (direction.magnitude > 0.1f)
                {
                    ctx.transform.rotation = Quaternion.Slerp(
                        ctx.transform.rotation,
                        Quaternion.LookRotation(direction),
                        ctx._rotationSpeed * Time.deltaTime);
                }

                // 攻撃クールダウン
                ctx._attackTimer -= Time.deltaTime;
                if (ctx._attackTimer <= 0f)
                {
                    if (ctx._animator != null)
                    {
                        ctx._animator.SetTrigger(AttackHash);
                    }
                    ctx._attackTimer = ctx._attackCooldown;
                }
            }
        }

        /// <summary>
        /// ヒットスタン状態
        /// </summary>
        private class HitStunState : EnemyStateBase
        {
            public override void Enter()
            {
                var ctx = Context;
                if (ctx._navAgent != null && ctx._navAgent.isOnNavMesh)
                {
                    ctx._navAgent.isStopped = true;
                }

                if (ctx._animator != null)
                {
                    ctx._animator.SetFloat(SpeedHash, 0f);
                }
            }

            public override void Update()
            {
                // HitStun中でもダメージは受ける（死亡判定のため）
                if (CheckDamageAndTransition()) return;

                var ctx = Context;
                ctx._hitStunTimer -= Time.deltaTime;

                if (ctx._hitStunTimer <= 0f)
                {
                    StateMachine.Transition(EnemyEvent.RecoverFromHit);
                }
            }
        }

        /// <summary>
        /// 死亡状態
        /// </summary>
        private class DeathState : State<SurvivorEnemyController, EnemyEvent>
        {
            public override void Enter()
            {
                Context.PerformDeath();
            }
        }

        /// <summary>
        /// 死亡処理実行
        /// </summary>
        private void PerformDeath()
        {
            if (_isDead) return;

            _isDead = true;

            if (_navAgent != null)
            {
                _navAgent.enabled = false;
            }

            if (_collider != null)
            {
                _collider.enabled = false;
            }

            if (_animator != null)
            {
                _animator.SetTrigger(DeathHash);
            }

            _onDeath.OnNext(this);
        }

        /// <summary>
        /// 外部からのダメージ処理（フラグを立てるのみ）
        /// </summary>
        private void TakeDamageWithStateMachine(int damage)
        {
            if (_isDead) return;
            RequestDamage(damage);
        }
    }
}
