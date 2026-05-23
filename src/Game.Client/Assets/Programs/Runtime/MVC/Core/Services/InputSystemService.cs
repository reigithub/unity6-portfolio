using System;
using Game.Shared.Input;
using UnityEngine;

namespace Game.Core.Services
{
    public class InputSystemService : IInputSystemService, IDisposable
    {
        private ProjectDefaultInputSystem _inputSystem;
        private bool _isPlayerEnabled;
        private bool _isUIEnabled;
        private bool _isInitialized;

        public ProjectDefaultInputSystem.PlayerActions Player => _inputSystem.Player;
        public ProjectDefaultInputSystem.UIActions UI => _inputSystem.UI;

        public InputSystemService()
        {
        }

        public void Startup()
        {
            if (_isInitialized) return;

            _inputSystem = new ProjectDefaultInputSystem();
            _inputSystem.Enable();

            // デフォルトでUI入力を有効化
            EnableUI();

            _isInitialized = true;
            Debug.Log("[InputService] Initialized");
        }

        public void Shutdown()
        {
            if (!_isInitialized) return;

            DisablePlayer();
            DisableUI();
            _inputSystem?.Dispose();
            _inputSystem = null;
            _isInitialized = false;

            Debug.Log("[InputService] Shutdown");
        }

        public void EnablePlayer()
        {
            if (Player.enabled) return;
            Player.Enable();
        }

        public void DisablePlayer()
        {
            if (!Player.enabled) return;
            Player.Disable();
        }

        public void EnableUI()
        {
            if (UI.enabled) return;
            UI.Enable();
        }

        public void DisableUI()
        {
            if (!UI.enabled) return;
            UI.Disable();
        }

        public void Dispose()
        {
            Shutdown();
        }
    }
}
