using Fusion.Addons.FSM;
using UnityEngine;

namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// プレイヤー死亡状態。死亡通知を発行し、サーバーに RPC を送信。
    /// </summary>
    public class SurvivorPlayerDeadState : StateBehaviour
    {
        private SurvivorFusionPlayer _player;

        public void Initialize(SurvivorFusionPlayer player)
        {
            _player = player;
        }

        protected override void OnEnterState()
        {
            Debug.Log("[SurvivorPlayerDeadState] Player died");

            // InputAuthority が死亡通知を送信 → サーバーが全クライアントにブロードキャスト
            // (Host-safe: Server 経路では直接 GameState を呼び、Client 経路では RPC を送信)
            if (_player.HasInputAuthority)
            {
                _player.SendClientPlayerDied();
            }
        }
    }
}
