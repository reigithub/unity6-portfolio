using UnityEngine;

namespace Game.ScoreTimeAttack.Player
{
    /// <summary>
    /// プレイヤーの衝突イベントを処理するインターフェース
    /// </summary>
    public interface IPlayerCollisionHandler
    {
        /// <summary>
        /// プレイヤーがトリガーに入った時の処理
        /// </summary>
        /// <param name="other">衝突したコライダー</param>
        void HandlePlayerTriggerEnter(Collider other);

        /// <summary>
        /// プレイヤーが衝突した時の処理
        /// </summary>
        /// <param name="collision">衝突情報</param>
        void HandlePlayerCollisionEnter(Collision collision);
    }
}
