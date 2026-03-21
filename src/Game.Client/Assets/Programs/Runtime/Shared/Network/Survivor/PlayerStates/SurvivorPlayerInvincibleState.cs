using Fusion.Addons.FSM;
using UnityEngine;

namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// プレイヤー無敵状態。タイマー終了で通常状態に戻る。無敵中もダメージフラグは消費する（無視）。
    /// </summary>
    public class SurvivorPlayerInvincibleState : StateBehaviour
    {
        private SurvivorFusionPlayer _player;
        private SurvivorPlayerNormalState _normalState;
        private SurvivorPlayerDeadState _deadState;

        public void Initialize(SurvivorFusionPlayer player,
            SurvivorPlayerNormalState normalState,
            SurvivorPlayerDeadState deadState)
        {
            _player = player;
            _normalState = normalState;
            _deadState = deadState;
        }

        protected override void OnEnterState()
        {
            _player.IsInvincible = true;
            _player.InvincibilityTimer = _player.InvincibilityDuration;
        }

        protected override void OnFixedUpdate()
        {
            // 無敵中のダメージフラグは消費して無視
            if (_player.HasPendingDamage)
            {
                _player.ConsumePendingDamage();
            }

            _player.InvincibilityTimer -= Runner.DeltaTime;

            if (_player.InvincibilityTimer <= 0f)
            {
                _player.InvincibilityTimer = 0f;
                Machine.TryActivateState(_normalState.StateId);
            }
        }

        protected override void OnExitState()
        {
            _player.IsInvincible = false;
        }
    }
}
