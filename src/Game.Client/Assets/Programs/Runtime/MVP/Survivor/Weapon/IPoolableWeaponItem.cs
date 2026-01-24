namespace Game.MVP.Survivor.Weapon
{
    /// <summary>
    /// プール可能な武器アイテムのインターフェース
    /// Projectile、GroundDamageArea等で実装
    /// </summary>
    public interface IPoolableWeaponItem
    {
        /// <summary>
        /// イベントリスナーをクリア（プール破棄時に呼ばれる）
        /// </summary>
        void ClearListeners();
    }
}
