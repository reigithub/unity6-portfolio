using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Horror.Services.Interfaces;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Game.Horror.Services
{
    public class HorrorIconService : IHorrorIconService
    {
        private const string UiIconsKey1 = "ModernGDR_Icons_BrightBackground";
        private const string WeaponIconsKey1 = "Navidtbt_Weapon_Icons_Blue";

        private AsyncOperationHandle<IList<Sprite>> _uiIconsHandle;
        private AsyncOperationHandle<IList<Sprite>> _weaponIconsHandle;

        private Dictionary<string, Sprite> _uiIcons;
        private Dictionary<string, Sprite> _weaponIcons;

        public HorrorIconService()
        {
        }

        public async UniTask LoadAsync()
        {
            await LoadUiIconsAsync();
            await LoadWeaponIconsAsync();
        }

        public void Unload()
        {
            Addressables.Release(_uiIconsHandle);
            _uiIcons.Clear();
            _uiIcons = null;
            Addressables.Release(_weaponIconsHandle);
            _weaponIcons.Clear();
            _weaponIcons = null;
        }

        private async UniTask LoadUiIconsAsync()
        {
            _uiIconsHandle = Addressables.LoadAssetAsync<IList<Sprite>>(UiIconsKey1);
            var sprites = await _uiIconsHandle.ToUniTask();
            _uiIcons = new Dictionary<string, Sprite>(sprites.Count);
            foreach (var sprite in sprites) _uiIcons[sprite.name] = sprite;
        }

        private async UniTask LoadWeaponIconsAsync()
        {
            _weaponIconsHandle = Addressables.LoadAssetAsync<IList<Sprite>>(WeaponIconsKey1);
            var sprites = await _weaponIconsHandle.ToUniTask();
            _weaponIcons = new Dictionary<string, Sprite>(sprites.Count);
            foreach (var sprite in sprites) _weaponIcons[sprite.name] = sprite;
        }

        public Sprite GetSprite(string spriteName)
        {
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
