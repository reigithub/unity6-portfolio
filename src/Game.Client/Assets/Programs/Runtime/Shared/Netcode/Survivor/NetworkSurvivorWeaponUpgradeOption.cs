using Unity.Collections;
using Unity.Netcode;
using Game.Library.Shared.Dto;

namespace Game.Shared.Netcode.Survivor
{
    public struct NetworkSurvivorWeaponUpgradeOption : INetworkSerializable
    {
        public int WeaponId;
        public FixedString128Bytes WeaponName;
        public bool IsNewWeapon;
        public int CurrentLevel;
        public FixedString128Bytes Description;
        public FixedString128Bytes UpgradeEffect;
        public FixedString128Bytes IconAssetName;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref WeaponId);
            serializer.SerializeValue(ref WeaponName);
            serializer.SerializeValue(ref IsNewWeapon);
            serializer.SerializeValue(ref CurrentLevel);
            serializer.SerializeValue(ref Description);
            serializer.SerializeValue(ref UpgradeEffect);
            serializer.SerializeValue(ref IconAssetName);
        }

        public static NetworkSurvivorWeaponUpgradeOption FromDto(WeaponUpgradeOptionSnapshot dto)
        {
            return new NetworkSurvivorWeaponUpgradeOption
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
