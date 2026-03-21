using MessagePack;

namespace Game.Library.Shared.Dto
{
    /// <summary>
    /// 武器アップグレード選択肢スナップショット（最小構成）。
    /// 名前・説明・アイコンはクライアント側でマスターデータから索引する。
    /// </summary>
    [MessagePackObject]
    public class WeaponUpgradeOptionSnapshot
    {
        [Key(0)]
        public int WeaponId { get; set; }

        [Key(1)]
        public bool IsNewWeapon { get; set; }

        [Key(2)]
        public int CurrentLevel { get; set; }
    }
}
