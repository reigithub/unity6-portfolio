using Fusion;
using Game.Library.Shared.Dto;

namespace Game.Shared.Network.Survivor
{
    [System.Serializable]
    public struct SurvivorNetworkWeaponUpgradeOption : INetworkStruct
    {
        public int WeaponId;
        public NetworkString<_128> WeaponName;
        public NetworkBool IsNewWeapon;
        public int CurrentLevel;
        public NetworkString<_128> Description;
        public NetworkString<_128> UpgradeEffect;
        public NetworkString<_128> IconAssetName;

        public static SurvivorNetworkWeaponUpgradeOption FromDto(WeaponUpgradeOptionSnapshot dto)
        {
            return new SurvivorNetworkWeaponUpgradeOption
            {
                WeaponId = dto.WeaponId,
                WeaponName = dto.WeaponName,
                IsNewWeapon = dto.IsNewWeapon,
                CurrentLevel = dto.CurrentLevel,
                Description = dto.Description,
                UpgradeEffect = dto.UpgradeEffect,
                IconAssetName = dto.IconAssetName,
            };
        }

        public WeaponUpgradeOptionSnapshot ToDto()
        {
            return new WeaponUpgradeOptionSnapshot
            {
                WeaponId = WeaponId,
                WeaponName = WeaponName.ToString(),
                IsNewWeapon = IsNewWeapon,
                CurrentLevel = CurrentLevel,
                Description = Description.ToString(),
                UpgradeEffect = UpgradeEffect.ToString(),
                IconAssetName = IconAssetName.ToString(),
            };
        }
    }
}
