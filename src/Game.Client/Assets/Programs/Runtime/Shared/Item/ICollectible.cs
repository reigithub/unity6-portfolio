using UnityEngine;

namespace Game.Shared.Item
{
    /// <summary>
    /// 収集可能なオブジェクトのインターフェース
    /// プレイヤーが拾えるアイテムに実装
    /// </summary>
    public interface ICollectible
    {
        /// <summary>
        /// 収集済みかどうか
        /// </summary>
        bool IsCollected { get; }

        /// <summary>
        /// ターゲットへの吸引を開始
        /// </summary>
        /// <param name="target">吸引先のTransform</param>
        /// <param name="speed">吸引速度</param>
        void StartAttraction(Transform target, float speed);

        /// <summary>
        /// アイテムを収集する
        /// </summary>
        void Collect();
    }
}
