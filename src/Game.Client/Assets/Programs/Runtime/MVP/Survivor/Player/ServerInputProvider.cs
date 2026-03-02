using Game.Shared.Network.Survivor;
using Game.Shared.Survivor;
using UnityEngine;

namespace Game.MVP.Survivor.Player
{
    /// <summary>
    /// Server 用入力プロバイダー。NetworkSurvivorPlayerState の ServerRpc バッファから入力を消費。
    /// </summary>
    public class ServerInputProvider : ISurvivorPlayerInputProvider
    {
        private readonly SurvivorNetworkPlayerState _networkPlayerState;

        public ServerInputProvider(SurvivorNetworkPlayerState networkPlayerState)
        {
            _networkPlayerState = networkPlayerState;
        }

        public bool TryGetMoveInput(out Vector2 moveValue, out bool isSprinting)
        {
            if (_networkPlayerState.TryConsumeInput(out var moveX, out var moveY, out var sprint))
            {
                moveValue = new Vector2(moveX, moveY);
                isSprinting = sprint;
                return true;
            }

            moveValue = Vector2.zero;
            isSprinting = false;
            return false;
        }
    }
}
