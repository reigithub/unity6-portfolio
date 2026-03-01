using Game.Shared.Netcode.Survivor;
using Game.Shared.Services;
using Game.Shared.Survivor;
using UnityEngine;

namespace Game.MVP.Survivor.Player
{
    /// <summary>
    /// Client 用入力プロバイダー。入力を読み取り ServerRpc で送信。
    /// ローカルでの入力処理は行わない（サーバーが権威的に処理）。
    /// </summary>
    public class ClientInputProvider : IPlayerInputProvider
    {
        private readonly IInputService _inputService;
        private readonly NetworkSurvivorPlayerState _networkPlayerState;

        public ClientInputProvider(IInputService inputService, NetworkSurvivorPlayerState networkPlayerState)
        {
            _inputService = inputService;
            _networkPlayerState = networkPlayerState;
        }

        public bool TryGetInput(out Vector2 moveValue, out bool isSprinting)
        {
            moveValue = _inputService.Player.Move.ReadValue<Vector2>();
            isSprinting = _inputService.Player.LeftShift.IsPressed();
            _networkPlayerState.SendMoveInputServerRpc(moveValue.x, moveValue.y, isSprinting);
            return false; // ローカル処理不要 — サーバーが権威的に処理
        }
    }
}
