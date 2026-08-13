using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Horror.Services.Interfaces;
using Game.Shared.Services;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Game.Horror.Services
{
    public class HorrorIconService : IHorrorIconService
    {
        private readonly IAddressableAssetService _assetService;

        private const string UiIconsKey1 = "ModernGDR_Icons_BrightBackground";
        private const string KeyIconsKey = "KeyItemIcons";
        private const string MedicalIconsKey = "MedicalItemIcons";
        private const string MilitaryIconsKey = "MilitaryItemIcons";

        private AsyncOperationHandle<IList<Sprite>> _uiIconsHandle;
        private AsyncOperationHandle<IList<Sprite>> _keyIconsHandle;
        private AsyncOperationHandle<IList<Sprite>> _medicalIconsHandle;
        private AsyncOperationHandle<IList<Sprite>> _militaryIconsHandle;

        private Dictionary<string, Sprite> _uiIcons;
        private Dictionary<string, Sprite> _keyIcons;
        private Dictionary<string, Sprite> _medicalIcons;
        private Dictionary<string, Sprite> _militaryIcons;

        public HorrorIconService(IAddressableAssetService assetService)
        {
            _assetService = assetService;
        }

        public async UniTask LoadAsync()
        {
            await LoadUiIconsAsync();
            await LoadKeyIconsAsync();
            await LoadMedicalIconsAsync();
            await LoadMilitaryIconsAsync();
        }

        public void Unload()
        {
            _assetService.Release(_uiIconsHandle);
            _uiIcons.Clear();
            _uiIcons = null;

            _assetService.Release(_keyIconsHandle);
            _keyIcons.Clear();
            _keyIcons = null;

            _assetService.Release(_medicalIconsHandle);
            _medicalIcons.Clear();
            _medicalIcons = null;

            _assetService.Release(_militaryIconsHandle);
            _militaryIcons.Clear();
            _militaryIcons = null;
        }

        private async UniTask LoadUiIconsAsync()
        {
            _uiIconsHandle = _assetService.LoadAssetAsyncHandle<IList<Sprite>>(UiIconsKey1);
            var sprites = await _uiIconsHandle.ToUniTask();
            _uiIcons = new Dictionary<string, Sprite>(sprites.Count);
            foreach (var sprite in sprites) _uiIcons[sprite.name] = sprite;
        }

        private async UniTask LoadKeyIconsAsync()
        {
            _keyIconsHandle = _assetService.LoadAssetAsyncHandle<IList<Sprite>>(KeyIconsKey);
            var sprites = await _keyIconsHandle.ToUniTask();
            _keyIcons = new Dictionary<string, Sprite>(sprites.Count);
            foreach (var sprite in sprites) _keyIcons[sprite.name] = sprite;
        }

        private async UniTask LoadMedicalIconsAsync()
        {
            _medicalIconsHandle = _assetService.LoadAssetAsyncHandle<IList<Sprite>>(MedicalIconsKey);
            var sprites = await _medicalIconsHandle.ToUniTask();
            _medicalIcons = new Dictionary<string, Sprite>(sprites.Count);
            foreach (var sprite in sprites) _medicalIcons[sprite.name] = sprite;
        }

        private async UniTask LoadMilitaryIconsAsync()
        {
            _militaryIconsHandle = _assetService.LoadAssetAsyncHandle<IList<Sprite>>(MilitaryIconsKey);
            var sprites = await _militaryIconsHandle.ToUniTask();
            _militaryIcons = new Dictionary<string, Sprite>(sprites.Count);
            foreach (var sprite in sprites) _militaryIcons[sprite.name] = sprite;
        }

        public Sprite GetSprite(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName))
                return null;

            var path = spriteName.Split('/');
            var (group, name) = (path[0], path[1]);
            switch (group)
            {
                case "UI": return _uiIcons[name];
                case "Key": return _keyIcons[name];
                case "Medical": return _medicalIcons[name];
                case "Military": return _militaryIcons[name];
                default: return null;
            }
        }
    }
}
