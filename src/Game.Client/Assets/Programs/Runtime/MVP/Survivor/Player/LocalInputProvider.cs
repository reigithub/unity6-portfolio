using Game.Shared.Services;
using Game.Shared.Survivor;
using UnityEngine;

namespace Game.MVP.Survivor.Player
{
    /// <summary>
    /// SP / Host 用入力プロバイダー。IInputService から直接読み取り。
    /// </summary>
    public class LocalInputProvider : IPlayerInputProvider
    {
        private readonly IInputService _inputService;

        public LocalInputProvider(IInputService inputService)
        {
            _inputService = inputService;
        }

        public bool TryGetInput(out Vector2 moveValue, out bool isSprinting)
        {
            moveValue = _inputService.Player.Move.ReadValue<Vector2>();
            isSprinting = _inputService.Player.LeftShift.IsPressed();
            return true;
        }
    }
}
