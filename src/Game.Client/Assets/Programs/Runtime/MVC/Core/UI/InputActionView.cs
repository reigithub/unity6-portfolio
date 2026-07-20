using Game.Core.Services;
using Game.Shared.Constants;
using Game.Shared.Input;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Core.UI
{
    public class InputActionView : MonoBehaviour
    {
        [SerializeField] private bool _initializeOnStart = true;

        [Header("Identity")]
        [SerializeField] private string _controlScheme;
        [SerializeField] private string _actionMapName = InputActionMaps.UI;
        [SerializeField] private string _actionName;
        [SerializeField] private string _compositePartName;

        [Header("Display")]
        [SerializeField] private Image _actionIcon;

        private bool _initialized;
        private IInputSystemService _inputService;
        private IInputActionIconService _inputActionIconService;

        public InputAction InputAction => _inputService.FindInputAction(_actionMapName, _actionName);

        private void Start()
        {
            if (_initializeOnStart) Initialize();
        }

        public void Initialize()
        {
            if (_initialized) return;
            _inputService = GameServiceManager.Resolve<IInputSystemService>();
            _inputActionIconService = GameServiceManager.Resolve<IInputActionIconService>();
            _inputService.OnControlSchemeChanged.Subscribe(_ => OnDeviceChanged()).AddTo(this);
            _inputService.OnDeviceChanged
                .Where(x => !x.DeviceChange.IsDisconnected())
                .Subscribe(_ => OnDeviceChanged())
                .AddTo(this);
            OnDeviceChanged();
            _initialized = true;
        }

        private void OnDeviceChanged()
        {
            Sprite sprite = null;

            if (string.IsNullOrEmpty(_controlScheme) || string.Equals(_controlScheme, _inputService.ControlScheme))
            {
                var info = _inputService.GetBindingInfo(ResolveControlScheme(), _actionMapName, _actionName, _compositePartName, InputControlSchemes.KeyboardAndMouse);
                sprite = _inputActionIconService.GetSprite(info);
            }

            if (sprite != null)
            {
                _actionIcon.color = Color.white;
                _actionIcon.sprite = sprite;
            }
            else
            {
                _actionIcon.color = Color.clear;
                _actionIcon.sprite = null;
            }
        }

        private string ResolveControlScheme()
        {
            if (!string.IsNullOrEmpty(_controlScheme)) return _controlScheme;
            if (!string.IsNullOrEmpty(_inputService.ControlScheme)) return _inputService.ControlScheme;
            return InputControlSchemes.KeyboardAndMouse;
        }
    }
}
