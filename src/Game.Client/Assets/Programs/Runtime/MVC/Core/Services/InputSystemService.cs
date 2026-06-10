using System;
using System.Linq;
using Game.Shared.Bootstrap;
using Game.Shared.Constants;
using Game.Shared.Input;
using R3;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Core.Services
{
    public class InputSystemService : IInputSystemService, IDisposable
    {
        private ProjectDefaultInputSystem _inputSystem;
        private bool _isInitialized;

        public ProjectDefaultInputSystem.PlayerActions Player => _inputSystem.Player;
        public ProjectDefaultInputSystem.UIActions UI => _inputSystem.UI;

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
            if (go == null) return;

            _selectedGameObject = go;

            if (EventSystem.current.currentSelectedGameObject == go) return;

            EventSystem.current.SetSelectedGameObject(go);
        }

        public void UpdateControlScheme(string device)
        {
            _controlScheme = device;
            ResolveControlScheme(_selectedGameObject);
        }

        public void ResolveControlScheme(GameObject selectedGameObject = null)
        {
            switch (_controlScheme)
            {
                case InputConstants.Gamepad:
                case InputConstants.Joystick:
                {
                    ApplicationEvents.HideCursor();
                    ResolveSelectable(selectedGameObject);
                    break;
                }
                case InputConstants.KeyboardAndMouse:
                case InputConstants.Touch:
                case InputConstants.XR:
                {
                    ApplicationEvents.ShowCursor();
                    ResolveSelectable(selectedGameObject);
                    break;
                }
            }
        }

        public void Dispose()
        {
            Shutdown();
        }
    }
}
