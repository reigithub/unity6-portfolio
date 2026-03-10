using Game.Shared.Network.Survivor;
using Game.Shared.Services;
using UnityEngine;

namespace Game.MVP.Survivor.Player
{
    /// <summary>
    /// SP / Host 用入力プロバイダー。IInputService から直接読み取り。
    /// </summary>
    public class LocalInputProvider : ISurvivorPlayerInputProvider
    {
        private readonly IInputService _inputService;

        public LocalInputProvider(IInputService inputService)
        {
            _inputService = inputService;
        }

        public bool TryGetMoveInput(out Vector2 moveValue, out bool isSprinting, out float cameraRotationY)
        {
            moveValue = _inputService.Player.Move.ReadValue<Vector2>();
            isSprinting = _inputService.Player.LeftShift.IsPressed();
            var cam = Camera.main;
            cameraRotationY = cam != null ? cam.transform.eulerAngles.y : 0f;
            return true;
        }
    }
}
