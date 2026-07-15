using Fusion;
using Game.Library.Shared.Dto;

namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// 武器アップグレード選択肢（ネットワーク送信用最小構造体）。
    /// WeaponId からマスターデータで名前・説明・アイコンを索引できるため、
    /// RPC ペイロードを最小化する。
    /// </summary>
    [System.Serializable]
    public struct SurvivorNetworkWeaponUpgradeOption : INetworkStruct
    {
        public int WeaponId;
        public NetworkBool IsNewWeapon;
        public int CurrentLevel;

        public SurvivorNetworkWeaponUpgradeOption FromDto(WeaponUpgradeOptionSnapshot dto)
        {
            return new SurvivorNetworkWeaponUpgradeOption
            {
                WeaponId = dto.WeaponId,
                IsNewWeapon = dto.IsNewWeapon,
                CurrentLevel = dto.CurrentLevel,
            };
        }

        public WeaponUpgradeOptionSnapshot ToDto()
        {
            return new WeaponUpgradeOptionSnapshot
            {
                WeaponId = WeaponId,
                IsNewWeapon = IsNewWeapon,
                CurrentLevel = CurrentLevel,
            };
        }
    }
}
