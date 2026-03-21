namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// 武器適用リクエストの種別
    /// </summary>
    public enum SurvivorWeaponApplyType
    {
        AddOrUpgrade,
        Replace
    }

    /// <summary>
    /// 武器適用リクエスト（サーバー側で処理）
    /// </summary>
    public struct SurvivorWeaponApplyRequest
    {
        public int WeaponId;
        public bool IsNewWeapon;
        public SurvivorWeaponApplyType Type;
        public int RemoveWeaponId;
    }
}
