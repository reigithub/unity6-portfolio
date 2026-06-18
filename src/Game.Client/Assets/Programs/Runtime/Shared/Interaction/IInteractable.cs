using UnityEngine;

namespace Game.Shared.Interaction
{
    /// <summary>
    /// インタラクト可能なオブジェクトのインターフェース。
    /// 検出基準点（<see cref="CenterPosition"/>）・実行（<see cref="Interact"/>）・
    /// 視覚状態の切替（<see cref="SetHighlighted"/>）を提供する。
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// 検出の基準となる中心位置。プレイヤーからの距離計算に使用する。
        /// </summary>
        Vector3 CenterPosition { get; }

        /// <summary>
        /// インタラクトアクション実行時の効果。
        /// </summary>
        void Interact();

        /// <summary>
        /// ハイライト（インタラクト可能であることの視覚表現）を切り替える。
        /// </summary>
        void SetHighlighted(bool highlighted);
    }
}
