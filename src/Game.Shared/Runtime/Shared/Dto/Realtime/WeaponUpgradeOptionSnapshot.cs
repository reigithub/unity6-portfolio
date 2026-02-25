using MessagePack;

namespace Game.Library.Shared.Dto
{
    /// <summary>
    /// 武器アップグレード選択肢スナップショット
    /// </summary>
    [MessagePackObject]
    public class WeaponUpgradeOptionSnapshot
    {
        [Key(0)]
        public int WeaponId { get; set; }

        [Key(1)]
        public string WeaponName { get; set; } = string.Empty;

        [Key(2)]
        public bool IsNewWeapon { get; set; }

        [Key(3)]
        public int CurrentLevel { get; set; }

        [Key(4)]
        public string Description { get; set; } = string.Empty;

        [Key(5)]
        public string UpgradeEffect { get; set; } = string.Empty;

        [Key(6)]
        public string IconAssetName { get; set; } = string.Empty;
    }
}
