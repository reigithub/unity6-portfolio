namespace Game.Shared.Combat
{
    /// <summary>
    /// ダメージを受けることができるオブジェクトのインターフェース
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// ダメージを受ける
        /// </summary>
        /// <param name="damage">ダメージ量</param>
        void TakeDamage(int damage);

        /// <summary>
        /// 死亡しているかどうか
        /// </summary>
        bool IsDead { get; }
    }
}
