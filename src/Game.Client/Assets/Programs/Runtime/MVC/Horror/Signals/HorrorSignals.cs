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

        // --- Player ---

        public static class Player
        {
            /// <summary>
            /// プレイヤー被弾イベントのデータ（画面フラッシュ・低 HP 演出等のプレイヤー専用フィードバックに使用）。
            /// CurrentHealth はダメージ適用後の残 HP。
            /// </summary>
            public readonly struct Damaged
            {
                public readonly int Damage;
                public readonly int CurrentHealth;
                public readonly int MaxHealth;

                public Damaged(int damage, int currentHealth, int maxHealth)
                {
                    Damage = damage;
                    CurrentHealth = currentHealth;
                    MaxHealth = maxHealth;
                }
            }

            /// <summary>
            /// プレイヤー死亡イベントのデータ（エネミー知覚の断絶等に使用）。
            /// Position は死亡地点のワールド座標（将来の死体演出用）。
            /// </summary>
            public readonly struct Died
            {
                public readonly Vector3 Position;

                public Died(Vector3 position)
                {
                    Position = position;
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

        // --- Enemy ---

        public static class Enemy
        {
            /// <summary>
            /// エネミー死亡イベントのデータ（撃破記録の永続化・ドロップ品の出現に使用）。
            /// SpawnId は HorrorEnemySpawnMaster の Id（スポーンエントリの一意識別子）。
            /// Position は死亡地点のワールド座標（ドロップ品の出現位置）。
            /// </summary>
            public readonly struct Died
            {
                public readonly int SpawnId;
                public readonly Vector3 Position;

                public Died(int spawnId, Vector3 position)
                {
                    SpawnId = spawnId;
                    Position = position;
                }
            }

            /// <summary>
            /// エネミースポーングループ起動イベントのデータ（連鎖スポーンの実行に使用）。
            /// SpawnGroupId は HorrorEnemySpawnGroupMaster の Id。
            /// </summary>
            public readonly struct SpawnGroupActivated
            {
                public readonly int SpawnGroupId;

                public SpawnGroupActivated(int spawnGroupId)
                {
                    SpawnGroupId = spawnGroupId;
                }
            }
        }
    }
}
