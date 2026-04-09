using UnityEngine;

namespace Game.MVP.Survivor.Enemy
{
    /// <summary>
    /// 敵 Animator パラメータのハッシュ定数。
    /// SurvivorEnemyView / EcsEnemyProxy で共用し、定義の重複を排除する。
    /// </summary>
    public static class EnemyAnimatorHashes
    {
        public static readonly int Speed = Animator.StringToHash("Speed");
        public static readonly int Attack = Animator.StringToHash("Attack");
        public static readonly int Death = Animator.StringToHash("Death");
        public static readonly int Hit = Animator.StringToHash("Hit");
    }
}
