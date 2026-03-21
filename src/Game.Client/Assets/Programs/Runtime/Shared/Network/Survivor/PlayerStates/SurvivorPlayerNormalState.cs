using Fusion.Addons.FSM;
using UnityEngine;

namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// プレイヤー通常状態。ダメージを受けると無敵状態に遷移、HP=0 で死亡状態に遷移。
    /// </summary>
    public class SurvivorPlayerNormalState : StateBehaviour
    {
        private SurvivorFusionPlayer _player;
        private SurvivorPlayerInvincibleState _invincibleState;
        private SurvivorPlayerDeadState _deadState;

        public void Initialize(SurvivorFusionPlayer player,
            SurvivorPlayerInvincibleState invincibleState,
            SurvivorPlayerDeadState deadState)
        {
            _player = player;
            _invincibleState = invincibleState;
            _deadState = deadState;
        }

        protected override void OnFixedUpdate()
        {
            if (_player == null) return;
            if (!_player.HasPendingDamage) return;

            int damage = _player.ConsumePendingDamage();
            Debug.Log($"[NormalState] Consumed damage={damage}, Health={_player.Health}");
            if (damage <= 0) return;

            // HP 減算
            _player.Health = Mathf.Max(0, _player.Health - damage);
            Debug.Log($"[NormalState] After damage: Health={_player.Health}");

            // サーバー→クライアント: ダメージ通知（MessagePipe 経由で UI 更新）
            _player.NotifyDamaged(damage);

            if (_player.Health <= 0)
            {
                Machine.TryActivateState(_deadState.StateId);
            }
            else
            {
                Machine.TryActivateState(_invincibleState.StateId);
            }
        }
    }
}
