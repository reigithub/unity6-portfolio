using System.Collections.Generic;
using Game.Core.Services;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Game.Horror.Services
{
    public class HorrorIconService : IGameService
    {
        private const string UiIconsKey1 = "ModernGDR_Icons_BrightBackground";
        private const string WeaponIconsKey1 = "Navidtbt_Weapon_Icons_Blue";

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
            Addressables.Release(UiIconsKey1);
            Addressables.Release(WeaponIconsKey1);
            _uiIcons.Clear();
            _uiIcons = null;
            _weaponIcons.Clear();
            _weaponIcons = null;
        }

        private async UniTask LoadUiIconsAsync()
        {
            var sprites = await Addressables.LoadAssetAsync<IList<Sprite>>(UiIconsKey1);
            _uiIcons = new Dictionary<string, Sprite>(sprites.Count);
            foreach (var sprite in sprites) _uiIcons[sprite.name] = sprite;
        }

        private async UniTask LoadWeaponIconsAsync()
        {
            var sprites = await Addressables.LoadAssetAsync<IList<Sprite>>(WeaponIconsKey1);
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
