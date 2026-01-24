using UnityEngine;

namespace Game.Shared.Combat
{
    /// <summary>
    /// ターゲット可能なエンティティのインターフェース
    /// ロックオンやプロジェクタイルのターゲットとして使用
    /// </summary>
    public interface ITargetable
    {
        /// <summary>
        /// エンティティの中心位置（コライダーの中心など）
        /// </summary>
        Vector3 CenterPosition { get; }
    }
}
