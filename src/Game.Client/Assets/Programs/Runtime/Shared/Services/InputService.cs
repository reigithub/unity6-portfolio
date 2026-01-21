using System;
using Game.Shared.Input;
using UnityEngine;

namespace Game.Shared.Services
{
    /// <summary>
    /// 入力サービス実装
    /// ゲーム起動時に初期化され、全体で共有されるInputSystemを管理
    /// VContainerでSingletonとして登録して使用
    /// </summary>
    public class InputService : IInputService, IDisposable
    {
        private ProjectDefaultInputSystem _inputSystem;
        private bool _isPlayerEnabled;
        private bool _isUIEnabled;
        private bool _isInitialized;

        public ProjectDefaultInputSystem.PlayerActions Player => _inputSystem.Player;
        public ProjectDefaultInputSystem.UIActions UI => _inputSystem.UI;

        public InputService()
        {
        }

        /// <summary>
        /// サービス初期化
        /// VContainerから呼び出される
        /// </summary>
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

        /// <summary>
        /// サービス終了
        /// </summary>
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