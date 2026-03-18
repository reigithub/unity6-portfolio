using Game.Library.Shared;
using Game.Shared.Network.Survivor;
using Game.Shared.Signals.Survivor;
using UnityEngine;

namespace Game.MVP.Survivor.Player
{
    /// <summary>
    /// SurvivorPlayerController - StateMachine実装部分
    /// SDUnityChanPlayerControllerと同様のステートマシン構造
    /// </summary>
    public partial class SurvivorPlayerController
    {
        // StateMachine
        private StateMachine<SurvivorPlayerController, PlayerEvent> _stateMachine;

        // Event Flags (State内部からのみ参照)
        private bool _hasPendingDamage;
        private int _pendingDamageAmount;

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
            _onDamageReceived.OnNext(
                new SurvivorSignals.Player.DamageReceived(_pendingDamageAmount, _currentHp.Value));

            // Server / Host: ダメージ通知
            if (_runnerService != null && _runnerService.IsServer)
            {
                if (_runnerService.TryGet<SurvivorFusionGameState>(out var gs))
                    gs.NotifyPlayerDamaged(_pendingDamageAmount, _currentHp.Value);
            }

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
            }
        }

        /// <summary>
        /// 無敵状態: 移動可能だが被ダメージ不可
        /// </summary>
        private class InvincibleState : PlayerStateBase
        {
            public override void Enter()
            {
                Context._isInvincible.Value = true;
            }

            public override void Update()
            {
                Context.TryProcessDamage(out _);

                Context._invincibilityTimer -= Context._networkDeltaTime;
                if (Context._invincibilityTimer <= 0f)
                {
                    StateMachine.Transition(PlayerEvent.InvincibilityEnd);
                }
            }

            public override void Exit()
            {
                Context._isInvincible.Value = false;
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
                Debug.Log("[SurvivorPlayerController] Player died");
                ctx._onDied.OnNext(new SurvivorSignals.Player.Died());

                // RPC 経由で死亡通知をサーバーに送信（InputAuthority のみ）
                if (ctx._fusionPlayer != null && ctx._fusionPlayer.HasInputAuthority)
                {
                    ctx._fusionPlayer.RpcClientPlayerDied();
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
