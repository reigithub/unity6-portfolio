using Unity.Collections;
using Game.Library.Shared.Dto;

namespace Game.Shared.Network.Survivor
{
    public struct SurvivorNetworkWeaponUpgradeOption
    {
        public int WeaponId;
        public FixedString128Bytes WeaponName;
        public bool IsNewWeapon;
        public int CurrentLevel;
        public FixedString128Bytes Description;
        public FixedString128Bytes UpgradeEffect;
        public FixedString128Bytes IconAssetName;

        public static SurvivorNetworkWeaponUpgradeOption FromDto(WeaponUpgradeOptionSnapshot dto)
        {
            return new SurvivorNetworkWeaponUpgradeOption
            {
                WeaponId = dto.WeaponId,
                WeaponName = new FixedString128Bytes(dto.WeaponName),
                IsNewWeapon = dto.IsNewWeapon,
                CurrentLevel = dto.CurrentLevel,
                Description = new FixedString128Bytes(dto.Description),
                UpgradeEffect = new FixedString128Bytes(dto.UpgradeEffect),
                IconAssetName = new FixedString128Bytes(dto.IconAssetName),
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
