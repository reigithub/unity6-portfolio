namespace Game.Shared.Enums
{
    /// <summary>
    /// ホラーゲームの武器種別。装備武器の挙動分岐（ヒットスキャン射撃／投擲）の基点。
    /// </summary>
    public enum HorrorWeaponType
    {
        /// <summary>銃器：攻撃入力でヒットスキャン射撃を行う</summary>
        Gun = 0,

        /// <summary>投擲武器：エイム中の攻撃入力で投擲物を射出する</summary>
        Throwable = 1,
    }
}
