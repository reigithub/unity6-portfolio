using UnityEngine;

namespace Game.MVP.Survivor.Signals
{
    /// <summary>
    /// Survivorゲーム用のシグナル定義
    /// MessagePipeで使用するマーカー型
    /// </summary>
    public static class SurvivorSignals
    {
        /// <summary>
        /// プレイヤー関連シグナル
        /// </summary>
        public static class Player
        {
            public readonly struct Spawned
            {
                public readonly Transform PlayerTransform;

                public Spawned(Transform playerTransform)
                {
                    PlayerTransform = playerTransform;
                }
            }

            public readonly struct Died { }

            public readonly struct DamageReceived
            {
                public readonly int Damage;
                public readonly int RemainingHp;

                public DamageReceived(int damage, int remainingHp)
                {
                    Damage = damage;
                    RemainingHp = remainingHp;
                }
            }

            public readonly struct LevelUp
            {
                public readonly int NewLevel;

                public LevelUp(int newLevel)
                {
                    NewLevel = newLevel;
                }
            }

            public readonly struct ExperienceGained
            {
                public readonly int Amount;

                public ExperienceGained(int amount)
                {
                    Amount = amount;
                }
            }
        }

        /// <summary>
        /// 敵関連シグナル
        /// </summary>
        public static class Enemy
        {
            public readonly struct Spawned
            {
                public readonly int EnemyId;

                public Spawned(int enemyId)
                {
                    EnemyId = enemyId;
                }
            }

            public readonly struct Killed
            {
                public readonly int EnemyId;
                public readonly int Score;

                public Killed(int enemyId, int score)
                {
                    EnemyId = enemyId;
                    Score = score;
                }
            }
        }

        /// <summary>
        /// ウェーブ関連シグナル
        /// </summary>
        public static class Wave
        {
            public readonly struct Started
            {
                public readonly int WaveNumber;

                public Started(int waveNumber)
                {
                    WaveNumber = waveNumber;
                }
            }

            public readonly struct Completed
            {
                public readonly int WaveNumber;

                public Completed(int waveNumber)
                {
                    WaveNumber = waveNumber;
                }
            }
        }

        /// <summary>
        /// ゲーム状態関連シグナル
        /// </summary>
        public static class Game
        {
            public readonly struct Paused { }
            public readonly struct Resumed { }
            public readonly struct Victory { }
            public readonly struct GameOver { }
        }
    }
}
