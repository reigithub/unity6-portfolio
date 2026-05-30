using System;
using System.Linq;
using Game.Shared.Bootstrap;
using Game.Shared.Constants;
using Game.Shared.Input;
using R3;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

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

        private CompositeDisposable Disposables { get; } = new();

        private string _controlScheme = InputConstants.DefaultControlScheme;
        private GameObject _selectedGameObject;

        #region Setup

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

        #endregion

        public void ResolveSelectable(GameObject selectedGameObject = null)
        {
            var allSelectables = InputSystemHelper.GetAllSelectables();
            if (allSelectables.Length > 0)
            {
                GameObject go = null;
                bool found = false;

                if (selectedGameObject != null)
                {
                    foreach (var selectable in allSelectables)
                    {
                        if (!selectable.IsSelectable()) continue;
                        if (selectable.gameObject == selectedGameObject)
                        {
                            go = selectable.gameObject;
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    var firstSelectable = allSelectables.FirstOrDefault(x => x.IsSelectable());
                    if (firstSelectable != null) go = firstSelectable.gameObject;
                }

                SetSelectedGameObject(go);
                Debug.Log($"[InputService] Selected GameObject {go}");
                return;
            }

            SetSelectedGameObject(null);
            Debug.Log("[InputService] No Selectables found");
        }

        public GameObject GetSelectedGameObject()
        {
            return EventSystem.current.currentSelectedGameObject;
        }

        public void SetSelectedGameObject(GameObject go)
        {
            _selectedGameObject = go;

            if (!CanDeselectGameObject() && go == null)
                return;

            EventSystem.current.SetSelectedGameObject(go);
        }

        private bool CanDeselectGameObject()
            => _controlScheme is not (InputConstants.Gamepad or InputConstants.Joystick);

        public void SubscribeControlScheme(PlayerInput playerInput)
        {
            // playerInput.controlsChangedEvent.AddListener(UpdateControlScheme);
            // InputSystem.onEvent += (inputEventPtr, device) => { Debug.Log($"InputSystem InputDevice: {device}"); };
            // Keyboard.current / Mouse.current / Gamepad.current / Pointer.current / Touchscreen.current;

            // playerInput.SwitchCurrentControlScheme(InputConstants.Gamepad);

            Observable.EveryValueChanged(playerInput, input => input.currentControlScheme)
                .Subscribe(device =>
                {
                    Debug.Log($"PlayerInput InputDevice: {device}");
                    UpdateControlScheme(device);
                })
                .AddTo(Disposables);

            UpdateControlScheme(playerInput.currentControlScheme);
        }

        public void UpdateControlScheme(string device)
        {
            _controlScheme = device;
            ResolveControlScheme();
        }

        public void ResolveControlScheme()
        {
            switch (_controlScheme)
            {
                case InputConstants.Gamepad:
                case InputConstants.Joystick:
                {
                    ApplicationEvents.HideCursor();
                    ResolveSelectable(_selectedGameObject);
                    break;
                }
                case InputConstants.KeyboardAndMouse:
                case InputConstants.Touch:
                case InputConstants.XR:
                {
                    ApplicationEvents.ShowCursor();
                    ResolveSelectable();
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
