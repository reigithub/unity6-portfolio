using Game.Shared.Network.Survivor;
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

        public bool TryGetMoveInput(out Vector2 moveValue, out bool isSprinting, out float cameraRotationY)
        {
            if (_networkPlayerState.TryConsumeInput(out var moveX, out var moveY, out var sprint, out var camRotY))
            {
                moveValue = new Vector2(moveX, moveY);
                isSprinting = sprint;
                cameraRotationY = camRotY;
                return true;
            }

            moveValue = Vector2.zero;
            isSprinting = false;
            cameraRotationY = 0f;
            return false;
        }
    }
}
