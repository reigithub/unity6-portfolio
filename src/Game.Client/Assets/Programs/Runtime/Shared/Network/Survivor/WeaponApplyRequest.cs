namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// 武器適用リクエストの種別
    /// </summary>
    public enum WeaponApplyType
    {
        AddOrUpgrade,
        Replace
    }

    /// <summary>
    /// 武器適用リクエスト（サーバー側で処理）
    /// </summary>
    public struct WeaponApplyRequest
    {
        public int WeaponId;
        public bool IsNewWeapon;
        public WeaponApplyType Type;
        public int RemoveWeaponId;
    }
}
