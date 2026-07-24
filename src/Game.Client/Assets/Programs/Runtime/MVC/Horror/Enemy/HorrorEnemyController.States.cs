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

            /// <summary>のけぞりステート</summary>
            Stagger,

            /// <summary>死亡ステート</summary>
            Dead,
        }

        /// <summary>
        /// ステートマシンを構築し遷移テーブルを登録する。
        /// </summary>
        private void InitializeStateMachine()
        {
            _stateMachine = new StateMachine<HorrorEnemyController, StateEvent>(this);

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

            // Stagger から各ステートへの復帰遷移
            _stateMachine.AddTransition<StaggerState, InvestigateState>(StateEvent.Suspect);
            _stateMachine.AddTransition<StaggerState, ChaseState>(StateEvent.Spot);
            _stateMachine.AddTransition<StaggerState, WanderState>(StateEvent.GiveUp);

            _stateMachine.AddTransition<StaggerState>(StateEvent.Stagger);
            _stateMachine.AddTransition<DeathState>(StateEvent.Dead);

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

                if (ctx._perception.IsThreatConfirmed)
                {
                    StateMachine.Transition(StateEvent.Spot);
                    return;
                }

                if (ctx._perception.IsSuspiciousOrHigher)
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

                if (ctx._perception.IsSuspiciousOrHigher)
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
        /// 捜索状態。注意対象位置（視認・全種の音の最新）へ WalkSpeed で向かい、
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

                // 注意対象位置（視認・全種の音の最新）へ向かう。刺激履歴が皆無なら現在位置に留まり見回す
                Vector3 dest = ctx._perception.TryGetLastNoticedPosition(out var noticed)
                    ? noticed
                    : ctx.transform.position;

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
        /// 追跡状態。視認中はプレイヤー現在位置へ、視認喪失中（Alert 継続）はプレイヤー知覚位置
        /// （視認・足音・銃声の最新。デコイでは動かない）へ ChaseSpeed で追尾する。
        /// プレイヤー知覚が皆無のまま Alert に達した敵（デコイ音のみ）は注意対象位置へ突進する（意図した仕様）。
        /// 攻撃間合いに入ったら（視認中のみ）Attack へ、視認・警戒を喪失したら Investigate へ遷移する。
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

                if (ctx._perception.HasConfirmedSight)
                {
                    // 攻撃遷移は視認中のみ許可する（壁越し・プレイヤー死亡後の死体への攻撃遷移を防ぐ）
                    if (ctx.IsWithinAttackRange())
                    {
                        StateMachine.Transition(StateEvent.EnterAttack);
                        return;
                    }

                    // Debug.Log("[HorrorEnemyController] Chase -> HasConfirmedSight");
                    ctx.MoveToThrottled(ctx._player.transform.position);
                    return;
                }

                if (ctx._perception.IsThreatConfirmed)
                {
                    // 視認喪失中（Alert 継続）はプレイヤー知覚位置を追う。真位置は追わない＝壁越し追跡の防止
                    if (ctx._perception.TryGetLastPerceivedPlayerPosition(out var playerPos))
                    {
                        // Debug.Log("[HorrorEnemyController] Chase -> LastPerceivedPlayerPosition");
                        ctx.MoveToThrottled(playerPos);
                        return;
                    }

                    // プレイヤー知覚が皆無（着弾音・悲鳴のみで Alert 到達）なら注意対象位置へ突進する
                    if (ctx._perception.TryGetLastNoticedPosition(out var noticedPos))
                    {
                        // Debug.Log("[HorrorEnemyController] Chase -> LastNoticedPosition");
                        ctx.MoveToThrottled(noticedPos);
                        return;
                    }

                    // 刺激履歴が皆無（実質到達不能）は防御的に LostTarget へ落とす
                }

                // 視認・警戒が両方消えたら最終知覚位置を辿る Investigate へ
                // Debug.Log("[HorrorEnemyController] LostTarget");
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

                // プレイヤー死亡なら攻撃を打ち切る（AttackState だけ知覚を参照しないための直接ガード。
                // AttackDone→Chase 後は知覚断絶により LostTarget→Investigate へ自然遷移する）
                if (ctx._playerDamageable.IsDead)
                {
                    ctx.ResumeAgent();
                    StateMachine.Transition(StateEvent.AttackDone);
                    return;
                }

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
        /// StaggerDuration 経過後: 視認中または Alert なら ChaseState へ、Suspicious なら InvestigateState へ、
        /// それ以外は WanderState へ復帰する。
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

                ctx.ResumeAgent();

                if (ctx._perception.IsThreatConfirmed)
                    StateMachine.Transition(StateEvent.Spot);
                else if (ctx._perception.IsSuspiciousOrHigher)
                    StateMachine.Transition(StateEvent.Suspect);
                else
                    StateMachine.Transition(StateEvent.GiveUp);
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
            private float _delay = 10f;

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

            public override void Update()
            {
                _delay -= Time.unscaledDeltaTime;
                if (_delay <= 0f)
                    Context.gameObject.SetActive(false);
            }
        }

        #endregion
    }
}
