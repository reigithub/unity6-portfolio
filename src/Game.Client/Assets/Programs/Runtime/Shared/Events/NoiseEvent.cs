using UnityEngine;

namespace Game.Shared.Events
{
    /// <summary>
    /// 音の種類
    /// </summary>
    public enum NoiseType
    {
        /// <summary>足音</summary>
        Footstep,

        /// <summary>物体の音（ドアの開閉・物の落下など）</summary>
        Object,

        /// <summary>銃声</summary>
        Gunshot,

        /// <summary>悲鳴</summary>
        Scream,
    }

    /// <summary>
    /// 音イベントのデータ（聴覚 AI への通知に使用）
    /// </summary>
    public struct NoiseEvent
    {
        /// <summary>
        /// 音の発生位置
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        /// 音の大きさ（0.0=無音 〜 1.0=通常 〜 それ以上=特大）
        /// </summary>
        public float Loudness { get; set; }

        /// <summary>
        /// 音の種類
        /// </summary>
        public NoiseType Type { get; set; }

        public NoiseEvent(Vector3 position, float loudness, NoiseType type)
        {
            Position = position;
            Loudness = loudness;
            Type = type;
        }
    }
}
