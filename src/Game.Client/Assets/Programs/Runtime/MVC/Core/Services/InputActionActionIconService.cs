using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Shared.Enums;
using Game.Shared.Input;
using Game.Shared.Services;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Game.Core.Services
{
    public class InputActionActionIconService : IInputActionIconService, IGameService
    {
        private readonly IAddressableAssetService _assetService;

        private const string KeyboardAndMouse = "keyboard-&-mouse_sheet_default";
        private const string PlayStation = "playstation-series_sheet_default";
        private const string Xbox = "xbox-series_sheet_default";

        private AsyncOperationHandle<IList<Sprite>> _kbmHandle;
        private AsyncOperationHandle<IList<Sprite>> _psHandle;
        private AsyncOperationHandle<IList<Sprite>> _xboxHandle;

        private Dictionary<string, Sprite> _kbmIcons;
        private Dictionary<string, Sprite> _psIcons;
        private Dictionary<string, Sprite> _xboxIcons;

        public InputActionActionIconService(IAddressableAssetService assetService)
        {
            _assetService = assetService;
        }

        public async UniTask LoadAsync()
        {
            await LoadKbmIconsAsync();
            await LoadPsIconsAsync();
            await LoadXboxIconsAsync();
        }

        public void Unload()
        {
            _assetService.Release(_kbmHandle);
            _kbmIcons.Clear();
            _kbmIcons = null;

            _assetService.Release(_psHandle);
            _psIcons.Clear();
            _psIcons = null;

            _assetService.Release(_xboxHandle);
            _xboxIcons.Clear();
            _xboxIcons = null;
        }

        private async UniTask LoadKbmIconsAsync()
        {
            _kbmHandle = _assetService.LoadAssetAsyncHandle<IList<Sprite>>(KeyboardAndMouse);
            var sprites = await _kbmHandle.ToUniTask();
            _kbmIcons = new Dictionary<string, Sprite>(sprites.Count);
            foreach (var sprite in sprites) _kbmIcons[sprite.name] = sprite;
        }

        private async UniTask LoadPsIconsAsync()
        {
            _psHandle = _assetService.LoadAssetAsyncHandle<IList<Sprite>>(PlayStation);
            var sprites = await _psHandle.ToUniTask();
            _psIcons = new Dictionary<string, Sprite>(sprites.Count);
            foreach (var sprite in sprites) _psIcons[sprite.name] = sprite;
        }

        private async UniTask LoadXboxIconsAsync()
        {
            _xboxHandle = _assetService.LoadAssetAsyncHandle<IList<Sprite>>(Xbox);
            var sprites = await _xboxHandle.ToUniTask();
            _xboxIcons = new Dictionary<string, Sprite>(sprites.Count);
            foreach (var sprite in sprites) _xboxIcons[sprite.name] = sprite;
        }

        public Sprite GetSprite(string deviceLayoutName, string controlPath)
        {
            var deviceType = InputSystemHelper.GetInputDeviceType(deviceLayoutName);
            var identifier = deviceType.ToIdentifier();
            var spriteName = identifier + "_" + controlPath;
            switch (deviceType)
            {
                case InputDeviceType.Keyboard:
                case InputDeviceType.Mouse:
                    return _kbmIcons[spriteName];
                case InputDeviceType.PlayStation:
                    return _psIcons[spriteName];
                case InputDeviceType.Xbox:
                    return _xboxIcons[spriteName];
                default:
                    return null;
            }
        }
    }
}
