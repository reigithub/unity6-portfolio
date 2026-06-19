using UnityEngine;

namespace Game.Shared.Interaction
{
    /// <summary>
    /// インタラクト可能なオブジェクトのインターフェース。
    /// 検出基準点（<see cref="CenterPosition"/>）・実行（<see cref="Interact"/>）・
    /// 提示状態の反映（<see cref="SetInteractionState"/>）を提供する。
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// 検出の基準となる中心位置。プレイヤーからの距離計算と可視判定（視線の的）に使用する。
        /// </summary>
        Vector3 CenterPosition { get; }

        /// <summary>
        /// インタラクトアクション実行時の効果。
        /// </summary>
        void Interact();

        /// <summary>
        /// 提示状態を反映する。対象側がアウトラインやプロンプト表示を切り替える。
        /// <paramref name="viewCamera"/> は対象側プロンプトがビルボードで正対するための視点カメラ
        /// （検出器が保持する唯一の視点。<see cref="InteractionState.Hidden"/> 時は未使用で null 可）。
        /// </summary>
        void SetInteractionState(InteractionState state, Camera viewCamera);
    }
}
