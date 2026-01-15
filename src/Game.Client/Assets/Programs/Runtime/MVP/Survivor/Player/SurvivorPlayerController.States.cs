using Game.Shared;
using R3;
using UnityEngine;

namespace Game.MVP.Survivor.Player
{
    /// <summary>
    /// SurvivorPlayerController - StateMachine実装部分
    /// </summary>
    public partial class SurvivorPlayerController
    {
        // StateMachine
        private StateMachine<SurvivorPlayerController, PlayerEvent> _stateMachine;

        // Event Flags (State内部からのみ参照)
        private bool _hasPendingDamage;
        private int _pendingDamageAmount;

        // Animator hash for Death
        private static readonly int DeathHash = Animator.StringToHash("Death");

        private void InitializeStateMachine()
        {
            _stateMachine = new StateMachine<SurvivorPlayerController, PlayerEvent>(this);

            // 遷移テーブル構築
            // Normal -> Invincible (被ダメージ)
            _stateMachine.AddTransition<NormalState, InvincibleState>(PlayerEvent.TakeDamage);

            // Normal -> Dead (死亡)
            _stateMachine.AddTransition<NormalState, DeadState>(PlayerEvent.Die);

            // Invincible -> Normal (無敵解除)
            _stateMachine.AddTransition<InvincibleState, NormalState>(PlayerEvent.InvincibilityEnd);

            // Invincible -> Dead (死亡)
            _stateMachine.AddTransition<InvincibleState, DeadState>(PlayerEvent.Die);

            // 初期ステート
            _stateMachine.SetInitState<NormalState>();
        }

        /// <summary>
        /// 状態遷移イベント
        /// </summary>
        private enum PlayerEvent
        {
            TakeDamage,       // 被ダメージ -> Invincible
            InvincibilityEnd, // 無敵解除 -> Normal
            Die               // 死亡 -> Dead
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
            if (_isInvincible.Value)
            {
                _hasPendingDamage = false;
                return false;
            }
            if (_currentHp.Value <= 0)
            {
                _hasPendingDamage = false;
                return false;
            }

            _hasPendingDamage = false;
            _currentHp.Value = Mathf.Max(0, _currentHp.Value - _pendingDamageAmount);
            _onDamaged.OnNext(_pendingDamageAmount);

            shouldDie = _currentHp.Value <= 0;
            if (!shouldDie)
            {
                _invincibilityTimer = _invincibilityDuration;
            }
            return true;
        }

        /// <summary>
        /// 基底State: 共通のダメージチェック
        /// </summary>
        private abstract class PlayerStateBase : State<SurvivorPlayerController, PlayerEvent>
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
                        StateMachine.Transition(PlayerEvent.Die);
                    }
                    else
                    {
                        StateMachine.Transition(PlayerEvent.TakeDamage);
                    }
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 通常状態: 移動・攻撃・被ダメージ可能
        /// </summary>
        private class NormalState : PlayerStateBase
        {
            public override void Update()
            {
                if (CheckDamageAndTransition()) return;

                var ctx = Context;
                ctx.HandleInput();
                ctx.HandleMovement();
            }
        }

        /// <summary>
        /// 無敵状態: 移動可能だが被ダメージ不可
        /// </summary>
        private class InvincibleState : PlayerStateBase
        {
            public override void Enter()
            {
                var ctx = Context;
                ctx._isInvincible.Value = true;
            }

            public override void Update()
            {
                // 無敵中はダメージを受けないが、フラグはクリアする
                Context.TryProcessDamage(out _);

                var ctx = Context;
                ctx.HandleInput();
                ctx.HandleMovement();

                // 無敵タイマー
                ctx._invincibilityTimer -= Time.deltaTime;
                if (ctx._invincibilityTimer <= 0f)
                {
                    StateMachine.Transition(PlayerEvent.InvincibilityEnd);
                }
            }

            public override void Exit()
            {
                var ctx = Context;
                ctx._isInvincible.Value = false;
            }
        }

        /// <summary>
        /// 死亡状態: 操作不可
        /// </summary>
        private class DeadState : State<SurvivorPlayerController, PlayerEvent>
        {
            public override void Enter()
            {
                var ctx = Context;
                ctx._onDeath.OnNext(Unit.Default);

                if (ctx._animator != null)
                {
                    ctx._animator.SetTrigger(DeathHash);
                }
            }
        }

        /// <summary>
        /// 外部からのダメージ処理（フラグを立てるのみ）
        /// </summary>
        private void TakeDamageWithStateMachine(int damage)
        {
            if (_currentHp.Value <= 0) return;
            RequestDamage(damage);
        }
    }
}
