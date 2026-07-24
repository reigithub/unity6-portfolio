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
        private const string WeaponIconsKey1 = "Navidtbt_Weapon_Icons_Blue";

        private AsyncOperationHandle<IList<Sprite>> _uiIconsHandle;
        private AsyncOperationHandle<IList<Sprite>> _weaponIconsHandle;

        private Dictionary<string, Sprite> _uiIcons;
        private Dictionary<string, Sprite> _weaponIcons;

        public HorrorIconService(IAddressableAssetService assetService)
        {
            _assetService = assetService;
        }

        public async UniTask LoadAsync()
        {
            await LoadUiIconsAsync();
            await LoadWeaponIconsAsync();
        }

        public void Unload()
        {
            _assetService.Release(_uiIconsHandle);
            _uiIcons.Clear();
            _uiIcons = null;
            _assetService.Release(_weaponIconsHandle);
            _weaponIcons.Clear();
            _weaponIcons = null;
        }

        private async UniTask LoadUiIconsAsync()
        {
            _uiIconsHandle = _assetService.LoadAssetAsyncHandle<IList<Sprite>>(UiIconsKey1);
            var sprites = await _uiIconsHandle.ToUniTask();
            _uiIcons = new Dictionary<string, Sprite>(sprites.Count);
            foreach (var sprite in sprites) _uiIcons[sprite.name] = sprite;
        }

        private async UniTask LoadWeaponIconsAsync()
        {
            _weaponIconsHandle = _assetService.LoadAssetAsyncHandle<IList<Sprite>>(WeaponIconsKey1);
            var sprites = await _weaponIconsHandle.ToUniTask();
            _weaponIcons = new Dictionary<string, Sprite>(sprites.Count);
            foreach (var sprite in sprites) _weaponIcons[sprite.name] = sprite;
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
                case "Weapon": return _weaponIcons[name];
                default: return null;
            }
        }
    }
}
