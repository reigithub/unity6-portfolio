using UnityEngine;

namespace Game.Shared.Combat
{
    /// <summary>
    /// ノックバックを受けることができるオブジェクトのインターフェース
    /// </summary>
    public interface IKnockbackable
    {
        /// <summary>
        /// ノックバックを適用
        /// </summary>
        /// <param name="knockback">ノックバックベクトル（方向と力を含む）</param>
        void ApplyKnockback(Vector3 knockback);
    }
}
