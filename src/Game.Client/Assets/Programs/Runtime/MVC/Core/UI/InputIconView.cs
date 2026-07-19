using Game.Core.Services;
using Game.Shared.Constants;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.UI
{
    public class InputIconView : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string _actionMapName = InputActionMaps.UI;
        [SerializeField] private string _actionName;

        [Header("Display")]
        [SerializeField] private Image _actionIcon;

        private IInputSystemService _inputService;
        private IInputIconService _inputIconService;

        private string _deviceLayoutName;
        private string _controlPath;

        private void Start()
        {
            _inputService = GameServiceManager.Resolve<IInputSystemService>();
            _inputIconService = GameServiceManager.Resolve<IInputIconService>();
            _inputService.OnControlSchemeChanged.Subscribe(_ => OnDeviceChanged()).AddTo(this);
            _inputService.OnDeviceChanged.Subscribe(_ => OnDeviceChanged()).AddTo(this);
            OnDeviceChanged();
        }

        private void OnDeviceChanged()
        {
            (string deviceLayoutName, string controlPath) = _inputService.GetDeviceControlPath(_actionMapName, _actionName);
            var sprite = _inputIconService.GetSprite(deviceLayoutName, controlPath);
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
    }
}
