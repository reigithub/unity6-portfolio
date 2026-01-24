namespace Game.Shared.Combat
{
    /// <summary>
    /// 戦闘ターゲットのインターフェース
    /// ダメージとノックバックの両方を受けることができるエンティティに実装
    /// </summary>
    public interface ICombatTarget : ITargetable, IDamageable, IKnockbackable
    {
    }
}