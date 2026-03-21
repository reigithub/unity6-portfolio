using Game.Library.Shared;
using Game.Shared;
using Game.Shared.Combat;
using Game.Shared.Events;
using R3;
using UnityEngine;

namespace Game.MVP.Survivor.Enemy
{
    /// <summary>
    /// SurvivorEnemyController - StateMachine実装部分
    /// </summary>
    public partial class SurvivorEnemyController
    {
        // Combat Settings（値はInitialize()でマスターデータから設定）
        // _attackRange, _attackCooldown, _hitStunDuration, _rotationSpeed, _attackRangeExitMultiplier は本体クラスで定義

        // Cached target reference
        private IDamageable _damageableTarget;

        // Timers
        private float _attackTimer;
        private float _hitStunTimer;

        // Event Flags (State内部からのみ参照)
        private bool _hasPendingDamage;
        private int _pendingDamageAmount;

        // StateMachine
        private StateMachine<SurvivorEnemyController, EnemyEvent> _stateMachine;

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
            _stateMachine.AddTransition<HitStunState, HitStunState>(EnemyEvent.TakeHit);
            _stateMachine.AddTransition<HitStunState, ChaseState>(EnemyEvent.RecoverFromHit);
            _stateMachine.AddTransition<HitStunState, DeathState>(EnemyEvent.Die);
            _stateMachine.AddTransition<IdleState, DeathState>(EnemyEvent.Die);
            _stateMachine.AddTransition<ChaseState, DeathState>(EnemyEvent.Die);
            _stateMachine.AddTransition<AttackState, DeathState>(EnemyEvent.Die);

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
        /// 同フレーム内の複数ヒットは最大値を採用（EmitCount>1の重複防止）
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
            int appliedDamage = _pendingDamageAmount;
            _currentHp -= _pendingDamageAmount;
            _hitStunTimer = _hitStunDuration;
            Debug.Log($"[EnemyDmg] id={EnemyId} applied={appliedDamage} hp={_currentHp + appliedDamage}→{_currentHp}");

            _onHitReceived.OnNext(Unit.Default);

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

                ctx.CurrentAnimationState = EnemyAnimationState.Idle;
                ctx._onAnimationStateChanged.OnNext(EnemyAnimationState.Idle);
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

                ctx.CurrentAnimationState = EnemyAnimationState.Chase;
                ctx._onAnimationStateChanged.OnNext(EnemyAnimationState.Chase);
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

                float sqrDistance = (ctx.transform.position - ctx._target.position).sqrMagnitude;
                if (sqrDistance <= ctx._attackRange * ctx._attackRange)
                {
                    StateMachine.Transition(EnemyEvent.EnterAttackRange);
                    return;
                }

                if (ctx._navAgent != null && ctx._navAgent.isOnNavMesh)
                {
                    ctx._navAgent.SetDestination(ctx._target.position);
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
                Debug.Log($"[Enemy:{ctx._enemyId}] AttackState.Enter: cooldown={ctx._attackCooldown:F2}");
                if (ctx._navAgent != null && ctx._navAgent.isOnNavMesh)
                {
                    ctx._navAgent.isStopped = true;
                }

                // 攻撃クールダウンを初期化（モーション再生後にダメージが発生するように）
                ctx._attackTimer = ctx._attackCooldown;

                ctx.CurrentAnimationState = EnemyAnimationState.Attack;
                ctx._onAnimationStateChanged.OnNext(EnemyAnimationState.Attack);
            }

            public override void Update()
            {
                if (CheckDamageAndTransition()) { Debug.Log($"[Enemy:{Context._enemyId}] AttackState: exiting via damage transition"); return; }

                var ctx = Context;

                if (ctx._target == null)
                {
                    Debug.Log($"[Enemy:{ctx._enemyId}] AttackState: target lost");
                    StateMachine.Transition(EnemyEvent.LostTarget);
                    return;
                }

                float sqrDistance = (ctx.transform.position - ctx._target.position).sqrMagnitude;
                float exitRange = ctx._attackRange * ctx._attackRangeExitMultiplier;
                if (sqrDistance > exitRange * exitRange)
                {
                    Debug.Log($"[Enemy:{ctx._enemyId}] AttackState: ExitAttackRange dist={Mathf.Sqrt(sqrDistance):F2} > exitRange={exitRange:F2} (range={ctx._attackRange:F2} * mult={ctx._attackRangeExitMultiplier:F2})");
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
                    // 攻撃実行
                    ctx.PerformAttack();
                    ctx._attackTimer = ctx._attackCooldown;
                }
            }
        }

        /// <summary>
        /// 攻撃実行（アニメーション + ダメージ）
        /// </summary>
        private void PerformAttack()
        {
            if (_target == null) { Debug.Log($"[Enemy:{_enemyId}] PerformAttack: _target is null"); return; }

            if (_damageableTarget == null)
            {
                _damageableTarget = _target.GetComponent<IDamageable>();
            }

            if (_damageableTarget == null) { Debug.Log($"[Enemy:{_enemyId}] PerformAttack: _damageableTarget is null on {_target.name}"); return; }
            if (_damageableTarget.IsDead) { Debug.Log($"[Enemy:{_enemyId}] PerformAttack: target IsDead=true"); return; }

            float sqrDistance = (transform.position - _target.position).sqrMagnitude;
            if (sqrDistance <= _attackRange * _attackRange)
            {
                Debug.Log($"[Enemy:{_enemyId}] PerformAttack: dealing {_attackDamage} damage, dist={Mathf.Sqrt(sqrDistance):F2}");
                _damageableTarget.TakeDamage(_attackDamage);
            }
            else
            {
                Debug.Log($"[Enemy:{_enemyId}] PerformAttack: out of range dist={Mathf.Sqrt(sqrDistance):F2} > range={_attackRange:F2}");
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

                ctx.CurrentAnimationState = EnemyAnimationState.HitStun;
                ctx._onAnimationStateChanged.OnNext(EnemyAnimationState.HitStun);
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

            CurrentAnimationState = EnemyAnimationState.Death;
            _onAnimationStateChanged.OnNext(EnemyAnimationState.Death);

            // ゲームロジック（イベント）は常に実行
            _onDeath.OnNext(this);

            _onDeathEvent.OnNext(new DeathEventData(
                transform.position,
                _itemDropGroupId,
                _expDropGroupId
            ));
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
