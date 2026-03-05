using Game.Shared.Network.Survivor;
using UnityEngine;

namespace Game.Shared.Signals.Survivor
{
    /// <summary>
    /// Survivor ゲームイベントシグナル定義（統一版）。
    /// SP: ゲームロジックが直接 Publish → SubscribeSignals で受信。
    /// MP Server: ゲームロジックが Publish → SubscribeSignals + Bridge → ClientRpc。
    /// MP Client: ClientRpc → NetworkSurvivorGameManager が Publish → SubscribeSignals で受信。
    /// </summary>
    public static class SurvivorSignals
    {
        // --- Session ---

        public static class Session
        {
            public readonly struct AllPlayersReady { }

            public readonly struct GameStarted
            {
                public readonly float ServerTime;

                public GameStarted(float serverTime)
                {
                    ServerTime = serverTime;
                }
            }
        }

        // --- Connection ---

        public static class Connection
        {
            public readonly struct PlayerConnected
            {
                public readonly string UserId;
                public readonly string PlayerName;

                public PlayerConnected(string userId, string playerName)
                {
                    UserId = userId;
                    PlayerName = playerName;
                }
            }

            public readonly struct PlayerDisconnected
            {
                public readonly string UserId;
                public readonly string PlayerName;

                public PlayerDisconnected(string userId, string playerName)
                {
                    UserId = userId;
                    PlayerName = playerName;
                }
            }
        }

        // --- Player ---

        public static class Player
        {
            /// <summary>プレイヤースポーン（ローカル専用 — カメラ追従用）</summary>
            public readonly struct Spawned
            {
                public readonly Transform PlayerTransform;

                public Spawned(Transform playerTransform)
                {
                    PlayerTransform = playerTransform;
                }
            }

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

            public readonly struct Died { }

            public readonly struct ItemCollected
            {
                public readonly string UserId;
                public readonly int ItemId;
                public readonly int EffectValue;

                public ItemCollected(string userId, int itemId, int effectValue)
                {
                    UserId = userId;
                    ItemId = itemId;
                    EffectValue = effectValue;
                }
            }

            public readonly struct LeveledUp
            {
                public readonly string UserId;
                public readonly int Level;
                public readonly SurvivorNetworkWeaponUpgradeOption[] Options;

                public LeveledUp(string userId, int level, SurvivorNetworkWeaponUpgradeOption[] options)
                {
                    UserId = userId;
                    Level = level;
                    Options = options;
                }
            }

            public readonly struct WeaponChanged
            {
                public readonly string UserId;
                public readonly int WeaponId;
                public readonly int Level;
                public readonly bool IsNew;

                public WeaponChanged(string userId, int weaponId, int level, bool isNew)
                {
                    UserId = userId;
                    WeaponId = weaponId;
                    Level = level;
                    IsNew = isNew;
                }
            }
        }

        // --- Enemy ---

        public static class Enemy
        {
            public readonly struct Killed
            {
                public readonly string KillerUserId;
                public readonly int EnemyId;
                public readonly int ScoreGained;
                public readonly int TotalKills;

                public Killed(string killerUserId, int enemyId, int scoreGained, int totalKills)
                {
                    KillerUserId = killerUserId;
                    EnemyId = enemyId;
                    ScoreGained = scoreGained;
                    TotalKills = totalKills;
                }
            }

            public readonly struct BatchUpdated
            {
                public readonly SurvivorNetworkEnemyStateSnapshot[] Enemies;

                public BatchUpdated(SurvivorNetworkEnemyStateSnapshot[] enemies)
                {
                    Enemies = enemies;
                }
            }
        }

        // --- Wave ---

        public static class Wave
        {
            public readonly struct Started
            {
                public readonly int WaveNumber;
                public readonly int TargetKillCount;
                public readonly int EnemyCount;

                public Started(int waveNumber, int targetKillCount, int enemyCount)
                {
                    WaveNumber = waveNumber;
                    TargetKillCount = targetKillCount;
                    EnemyCount = enemyCount;
                }
            }

            /// <summary>
            /// SP/Server: WaveClearScore=0（消費者がローカル計算）
            /// MP Client: WaveClearScore=サーバー計算済み値
            /// </summary>
            public readonly struct Completed
            {
                public readonly int WaveNumber;
                public readonly int WaveClearScore;

                public Completed(int waveNumber)
                {
                    WaveNumber = waveNumber;
                    WaveClearScore = 0;
                }

                public Completed(int waveNumber, int waveClearScore)
                {
                    WaveNumber = waveNumber;
                    WaveClearScore = waveClearScore;
                }
            }

            public readonly struct AllCleared { }

            public readonly struct TimeUp { }
        }

        // --- Game ---

        public static class Game
        {
            public readonly struct Ended
            {
                public readonly SurvivorNetworkGameResult Result;

                public Ended(SurvivorNetworkGameResult result)
                {
                    Result = result;
                }
            }

            public readonly struct Paused
            {
                public readonly string RequestedByUserId;

                public Paused(string requestedByUserId)
                {
                    RequestedByUserId = requestedByUserId;
                }
            }

            public readonly struct Resumed { }
        }

        // --- Item ---

        public static class Item
        {
            public readonly struct Spawned
            {
                public readonly int ItemId;
                public readonly float PosX;
                public readonly float PosZ;

                public Spawned(int itemId, float posX, float posZ)
                {
                    ItemId = itemId;
                    PosX = posX;
                    PosZ = posZ;
                }
            }

            public readonly struct Despawned
            {
                public readonly int ItemId;

                public Despawned(int itemId)
                {
                    ItemId = itemId;
                }
            }
        }
    }
}
