using System;
using Game.Shared.Bootstrap;
using Game.Shared.Constants;
using Game.Shared.Input;
using Game.Shared.Services;
using R3;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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

        public string ControlScheme { get; private set; } = InputConstants.DefaultControlScheme;
        public CompositeDisposable Disposables { get; } = new();

        private GameObject _selectedGameObject;
        private IDisposable _selectableDisposable;

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

        public IDisposable BlockPlayer()
        {
            DisablePlayer();
            return Disposable.Create(() => EnablePlayer());
        }

        public IDisposable BlockUI()
        {
            DisableUI();
            return Disposable.Create(() => EnableUI());
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

        public void SubscribeSelectable()
        {
            DisposeSelectable();
            _selectableDisposable = Observable.EveryValueChanged(EventSystem.current, system => system.currentSelectedGameObject)
                .Subscribe(go =>
                {
                    if (go != null) Debug.Log($"EventSystem SelectedGameObject: {go.name}");
                    SetSelectedGameObject(go);
                })
                .AddTo(Disposables);
        }

        public void DisposeSelectable() => _selectableDisposable?.Dispose();

        public void ResolveSelectable(Selectable[] selectables = null)
        {
            var allSelectables = InputSystemHelper.GetAllSelectables(selectables);
            if (allSelectables.Length > 0)
            {
                var go = allSelectables[0].gameObject;
                // if (_selectedGameObject != null)
                // {
                //     foreach (var selectable in allSelectables)
                //     {
                //         if (_selectedGameObject == selectable.gameObject)
                //         {
                //             go = selectable.gameObject;
                //         }
                //     }
                // }
                SetSelectedGameObject(go);
            }
        }

        public void SetSelectedGameObject(GameObject go)
        {
            if (go != null) _selectedGameObject = go;
            EventSystem.current.SetSelectedGameObject(go);
        }


        public void SubscribeControlScheme(PlayerInput playerInput)
        {
            Observable.EveryValueChanged(playerInput, input => input.currentControlScheme)
                .Subscribe(device =>
                {
                    Debug.Log($"PlayerInput InputDevice: {device}");
                    UpdateControlScheme(device);
                })
                .AddTo(Disposables);
        }

        public void UpdateControlScheme(string device)
        {
            ControlScheme = device;

            switch (device)
            {
                case InputConstants.Gamepad:
                {
                    ApplicationEvents.HideCursor();
                    ResolveSelectable();
                    break;
                }
                case InputConstants.KeyboardAndMouse:
                default:
                {
                    ApplicationEvents.ShowCursor();
                    SetSelectedGameObject(null);
                    break;
                }
            }
        }

        public void Dispose()
        {
            Disposables?.Dispose();
            Shutdown();
        }
    }
}
