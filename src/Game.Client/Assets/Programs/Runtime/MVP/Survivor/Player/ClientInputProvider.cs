using Game.Shared.Network.Survivor;
using Game.Shared.Services;
using UnityEngine;

namespace Game.MVP.Survivor.Player
{
    /// <summary>
    /// Client 用入力プロバイダー。入力を読み取り ServerRpc で送信。
    /// ローカルでの入力処理は行わない（サーバーが権威的に処理）。
    /// </summary>
    public class ClientInputProvider : ISurvivorPlayerInputProvider
    {
        private readonly IInputService _inputService;
        private readonly SurvivorNetworkPlayerState _networkPlayerState;

        public ClientInputProvider(IInputService inputService, SurvivorNetworkPlayerState networkPlayerState)
        {
            _inputService = inputService;
            _networkPlayerState = networkPlayerState;
        }

        public bool TryGetMoveInput(out Vector2 moveValue, out bool isSprinting, out float cameraRotationY)
        {
            moveValue = _inputService.Player.Move.ReadValue<Vector2>();
            isSprinting = _inputService.Player.LeftShift.IsPressed();
            var cam = Camera.main;
            cameraRotationY = cam != null ? cam.transform.eulerAngles.y : 0f;
            _networkPlayerState.SendMoveInputServerRpc(moveValue.x, moveValue.y, isSprinting, cameraRotationY);
            return true; // ローカル予測: 即座に移動し、サーバー補正は SurvivorPlayerView で適用
        }
    }
}
