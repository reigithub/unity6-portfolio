using Game.Library.Shared;
using Game.Shared.Extensions;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Horror.Enemy
{
    public partial class HorrorEnemyController
    {
        /// <summary>
        /// 状態遷移イベントキー。
        /// Stagger/Death は TakeDamage 内の ForceTransition で割り込むため event 定義不要。
        /// </summary>
        private enum StateEvent
        {
            /// <summary>警戒検知（警戒度が SuspiciousThreshold 以上）→ InvestigateState へ</summary>
            Suspect,

            /// <summary>視認確定 または Alert レベル到達 → ChaseState へ</summary>
            Spot,

            /// <summary>攻撃間合い内 → AttackState へ</summary>
            EnterAttack,

            /// <summary>攻撃クールダウン完了かつ間合い外 → ChaseState へ</summary>
            AttackDone,

            /// <summary>視認・警戒喪失 → InvestigateState（LKP 追跡）へ</summary>
            LostTarget,

            /// <summary>捜索タイムアウト または Stagger 復帰で視認なし → WanderState へ</summary>
            GiveUp,
        }

        /// <summary>
        /// ステートマシンを構築し遷移テーブルを登録する。
        /// </summary>
        private void InitializeStateMachine()
        {
            _stateMachine = new EnemyStateMachine(this);

            // Dormant から各ステートへの遷移
            _stateMachine.AddTransition<DormantState, InvestigateState>(StateEvent.Suspect);
            _stateMachine.AddTransition<DormantState, ChaseState>(StateEvent.Spot);

            // Wander から各ステートへの遷移
            _stateMachine.AddTransition<WanderState, InvestigateState>(StateEvent.Suspect);
            _stateMachine.AddTransition<WanderState, ChaseState>(StateEvent.Spot);

            // Investigate から各ステートへの遷移
            _stateMachine.AddTransition<InvestigateState, ChaseState>(StateEvent.Spot);
            _stateMachine.AddTransition<InvestigateState, WanderState>(StateEvent.GiveUp);

            // Chase から各ステートへの遷移
            _stateMachine.AddTransition<ChaseState, AttackState>(StateEvent.EnterAttack);
            _stateMachine.AddTransition<ChaseState, InvestigateState>(StateEvent.LostTarget);

            // Attack から Chase への復帰（間合い外に出た）
            _stateMachine.AddTransition<AttackState, ChaseState>(StateEvent.AttackDone);

            // Stagger から各ステートへの復帰遷移（ForceTransition で入ってきた後）
            _stateMachine.AddTransition<StaggerState, ChaseState>(StateEvent.Spot);
            _stateMachine.AddTransition<StaggerState, WanderState>(StateEvent.GiveUp);

            if (_startDormant)
                _stateMachine.SetInitState<DormantState>();
            else
                _stateMachine.SetInitState<WanderState>();
        }

        #region State: Dormant（休眠）

        /// <summary>
        /// 休眠状態。エージェントを停止し、知覚が反応するまで動かない。
        /// 初期配置で周囲に気づいていない敵の開始状態として使用する。
        /// </summary>
        private class DormantState : State<HorrorEnemyController, StateEvent>
        {
            public override void Enter()
            {
                var ctx = Context;
                ctx.StopAgent();
                ctx.SetSpeed(0f);
            }

            public override void Update()
            {
                var ctx = Context;

                if (ctx._perception.HasConfirmedSight
                    || ctx._perception.Level == HorrorEnemyPerception.AwarenessLevel.Alert)
                {
                    StateMachine.Transition(StateEvent.Spot);
                    return;
                }

                if (ctx._perception.Level >= HorrorEnemyPerception.AwarenessLevel.Suspicious)
                {
                    StateMachine.Transition(StateEvent.Suspect);
                }
            }
        }

        #endregion

        #region State: Wander（徘徊）

        /// <summary>
        /// 徘徊状態。NavMesh 上のランダム点を WalkSpeed で巡回する。
        /// 知覚が反応したら Investigate または Chase へ遷移する。
        /// </summary>
        private class WanderState : State<HorrorEnemyController, StateEvent>
        {
            public override void Enter()
            {
                var ctx = Context;
                ctx.ResumeAgent();
                ctx.SetSpeed(ctx._master.WalkSpeed);
            }

            public override void Update()
            {
                var ctx = Context;

                if (ctx._perception.HasConfirmedSight)
                {
                    StateMachine.Transition(StateEvent.Spot);
                    return;
                }

                if (ctx._perception.Level >= HorrorEnemyPerception.AwarenessLevel.Suspicious)
                {
                    StateMachine.Transition(StateEvent.Suspect);
                    return;
                }

                ctx.WanderToRandomPoint();
            }
        }

        #endregion

        #region State: Investigate（捜索）

        /// <summary>
        /// 捜索状態。LastHeardPosition または LastKnownPosition へ WalkSpeed で向かい、
        /// 到着後は周囲を緩やかに見回す。
        /// InvestigateGiveUpTime 経過で諦めて WanderState へ戻る。
        /// </summary>
        private class InvestigateState : State<HorrorEnemyController, StateEvent>
        {
            private float _giveUpTimer;

            public override void Enter()
            {
                var ctx = Context;
                ctx.ResumeAgent();
                ctx.SetSpeed(ctx._master.WalkSpeed);
                _giveUpTimer = 0f;

                // 聴覚位置を優先し、なければ視覚の最終確認位置（LKP）へ向かう
                Vector3 dest = ctx._perception.LastHeardPosition != Vector3.zero
                    ? ctx._perception.LastHeardPosition
                    : ctx._perception.LastKnownPosition;

                if (ctx._navMeshAgent != null)
                {
                    float leniency = ctx._navMeshAgent.radius + ctx._navMeshAgent.stoppingDistance + ctx._navMeshAgent.height;
                    ctx._navMeshAgent.SetDestinationImmediate(dest, leniency);
                }

                ctx._lastDestination = dest;
            }

            public override void Update()
            {
                var ctx = Context;

                if (ctx._perception.HasConfirmedSight)
                {
                    StateMachine.Transition(StateEvent.Spot);
                    return;
                }

                _giveUpTimer += Time.deltaTime;
                if (_giveUpTimer >= ctx._master.InvestigateGiveUpTime)
                {
                    StateMachine.Transition(StateEvent.GiveUp);
                    return;
                }

                // 目標地点に到着したら周囲を見回す（Sin 波による左右スイング）
                if (!ctx._navMeshAgent.pathPending && ctx._navMeshAgent.remainingDistance < 1.5f)
                {
                    float swingAngle = Mathf.Sin(Time.time * 0.8f) * 60f;
                    ctx.transform.rotation = Quaternion.Slerp(
                        ctx.transform.rotation,
                        Quaternion.Euler(0f, ctx.transform.eulerAngles.y + swingAngle * Time.deltaTime, 0f),
                        2f * Time.deltaTime);
                }
            }
        }

        #endregion

        #region State: Chase（追跡）

        /// <summary>
        /// 追跡状態。プレイヤー現在位置へ ChaseSpeed で追尾する。
        /// Enter 時に Scream を Publish してホード伝播させる。
        /// 攻撃間合いに入ったら Attack へ、視認・警戒を喪失したら Investigate へ遷移する。
        /// </summary>
        private class ChaseState : State<HorrorEnemyController, StateEvent>
        {
            public override void Enter()
            {
                var ctx = Context;
                ctx.ResumeAgent();
                ctx.SetSpeed(ctx._master.ChaseSpeed);
                // タイマーを RepathInterval 以上にして最初のフレームで即座に目的地を設定する
                ctx._repathTimer = ctx._master.RepathInterval;
                // ctx.PublishScream();
            }

            public override void Update()
            {
                var ctx = Context;

                if (ctx.IsWithinAttackRange())
                {
                    StateMachine.Transition(StateEvent.EnterAttack);
                    return;
                }

                if (ctx._perception.HasConfirmedSight
                    || ctx._perception.Level == HorrorEnemyPerception.AwarenessLevel.Alert)
                {
                    ctx.MoveToThrottled(ctx._player.transform.position);
                    return;
                }

                // 視認・警戒が両方消えたら LKP を辿る Investigate へ
                StateMachine.Transition(StateEvent.LostTarget);
            }
        }

        #endregion

        #region State: Attack（攻撃）

        /// <summary>
        /// 攻撃状態。エージェントを停止し Attack トリガーを発火してダメージを与える。
        /// AttackCooldown 経過後: 間合い内なら継続攻撃、間合い外なら Chase へ戻る。
        /// </summary>
        private class AttackState : State<HorrorEnemyController, StateEvent>
        {
            private float _cooldownTimer;

            public override void Enter()
            {
                var ctx = Context;
                ctx.StopAgent();
                ctx.TriggerAttack();
                ctx.ApplyAttackDamage();
                _cooldownTimer = 0f;
            }

            public override void Update()
            {
                var ctx = Context;
                ctx.FaceTarget();

                _cooldownTimer += Time.deltaTime;
                if (_cooldownTimer < ctx._master.AttackCooldown) return;

                if (ctx.IsWithinAttackRange())
                {
                    // 継続攻撃
                    ctx.TriggerAttack();
                    ctx.ApplyAttackDamage();
                    _cooldownTimer = 0f;
                }
                else
                {
                    ctx.ResumeAgent();
                    StateMachine.Transition(StateEvent.AttackDone);
                }
            }
        }

        #endregion

        #region State: Stagger（のけぞり）

        /// <summary>
        /// のけぞり状態。TakeDamage から ForceTransition で割り込む。
        /// StaggerDuration 経過後: 視認中なら ChaseState へ、それ以外は WanderState へ復帰する。
        /// </summary>
        private class StaggerState : State<HorrorEnemyController, StateEvent>
        {
            private float _timer;

            public override void Enter()
            {
                var ctx = Context;
                ctx.StopAgent();
                ctx.TriggerStagger();
                _timer = 0f;
            }

            public override void Update()
            {
                var ctx = Context;
                _timer += Time.deltaTime;
                if (_timer < ctx._master.StaggerDuration) return;

                if (ctx._perception.HasConfirmedSight
                    || ctx._perception.Level == HorrorEnemyPerception.AwarenessLevel.Alert)
                {
                    ctx.ResumeAgent();
                    StateMachine.Transition(StateEvent.Spot);
                }
                else
                {
                    ctx.ResumeAgent();
                    StateMachine.Transition(StateEvent.GiveUp);
                }
            }
        }

        #endregion

        #region State: Death（死亡）

        /// <summary>
        /// 死亡状態。TakeDamage から ForceTransition で割り込む。終端状態。
        /// Death トリガーを発火し、NavMeshAgent とすべてのコライダーを無効化する。
        /// </summary>
        private class DeathState : State<HorrorEnemyController, StateEvent>
        {
            public override void Enter()
            {
                var ctx = Context;
                ctx.TriggerDeath();

                if (ctx._navMeshAgent)
                    ctx._navMeshAgent.enabled = false;

                // すべてのコライダーを無効化（プレイヤーとの衝突・攻撃判定を排除）
                var colliders = ctx.GetComponents<Collider>();
                foreach (var col in colliders)
                    col.enabled = false;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
                Debug.Log($"[HorrorEnemyController] DeathState: 敵が死亡した ({ctx.name})");
                // ctx.gameObject.SafeDestroy();
#endif
            }
        }

        #endregion
    }
}
