using UnityEngine;

namespace Game.Horror.Signals
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
    /// Horror ゲームイベントシグナル定義。
    /// IMessagePipeService で Publish/Subscribe する型を1箇所に集約する。
    /// 使用する型は HorrorGameLauncher で AddMessageBroker 登録が必要。
    /// </summary>
    public static class HorrorSignals
    {
        // --- Combat ---

        public static class Combat
        {
            /// <summary>
            /// ダメージ適用イベントのデータ（ヒット位置のダメージ数値ポップアップ表示等の演出に使用）。
            /// Position はヒット位置のワールド座標。
            /// </summary>
            public readonly struct Damaged
            {
                public readonly Vector3 Position;
                public readonly int Damage;

                public Damaged(Vector3 position, int damage)
                {
                    Position = position;
                    Damage = damage;
                }
            }
        }

        // --- Noise ---

        public static class Noise
        {
            /// <summary>
            /// 音イベントのデータ（聴覚 AI への通知に使用）。
            /// Position は音の発生位置、Loudness は音の大きさ（0.0=無音 〜 1.0=通常 〜 それ以上=特大）。
            /// </summary>
            public readonly struct Occurred
            {
                public readonly Vector3 Position;
                public readonly float Loudness;
                public readonly NoiseType Type;

                public Occurred(Vector3 position, float loudness, NoiseType type)
                {
                    Position = position;
                    Loudness = loudness;
                    Type = type;
                }
            }
        }
    }
}
