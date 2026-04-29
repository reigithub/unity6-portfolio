using Game.Shared.Network.Survivor;
using Game.Shared.Services;
using UnityEngine;

namespace Game.Shared.Signals.Survivor
{
    /// <summary>
    /// Survivor ゲームイベントシグナル定義。
    /// Server: ゲームロジックが Publish → SubscribeSignals + Bridge → ClientRpc。
    /// Client: ClientRpc → SurvivorFusionGameState が Publish → SubscribeSignals で受信。
    /// SP/MP の違いは接続先のみで、シグナル経路（Server / Client）は共通。
    /// </summary>
    public static class SurvivorSignals
    {
        // --- Auth ---

        public static class Auth
        {
            /// <summary>
            /// 認証 session の refresh 試行結果。
            /// <see cref="AuthSessionRefresher"/> が refresh 完了時に publish する (成功/失敗問わず)。
            /// </summary>
            public readonly struct SessionRefreshResult
            {
                public readonly bool IsSuccess;
                public readonly RefreshTrigger Trigger;
                public readonly string ErrorMessage;

                public SessionRefreshResult(bool isSuccess, RefreshTrigger trigger, string errorMessage)
                {
                    IsSuccess = isSuccess;
                    Trigger = trigger;
                    ErrorMessage = errorMessage;
                }
            }
        }

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

            public readonly struct AllClientsSceneReady { }

            public readonly struct ClientFieldSceneLoaded { }

            public readonly struct AllClientsFieldSceneLoaded { }

            public readonly struct AllPlayersDisconnected { }
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
                public readonly string UserId;
                public readonly int Damage;
                public readonly int RemainingHp;

                public DamageReceived(string userId, int damage, int remainingHp)
                {
                    UserId = userId;
                    Damage = damage;
                    RemainingHp = remainingHp;
                }
            }

            public readonly struct Died
            {
                public readonly string UserId;

                public Died(string userId)
                {
                    UserId = userId;
                }
            }

            public readonly struct ItemCollected
            {
                public readonly string UserId;
                public readonly int ItemId;
                public readonly int ItemType;
                public readonly int EffectValue;
                public readonly int CurrentExperience;
                public readonly int ExperienceToNextLevel;

                public ItemCollected(string userId, int itemId, int itemType, int effectValue,
                    int currentExperience, int experienceToNextLevel)
                {
                    UserId = userId;
                    ItemId = itemId;
                    ItemType = itemType;
                    EffectValue = effectValue;
                    CurrentExperience = currentExperience;
                    ExperienceToNextLevel = experienceToNextLevel;
                }
            }

            public readonly struct LeveledUp
            {
                public readonly string UserId;
                public readonly int Level;
                public readonly int Experience;
                public readonly int ExperienceToNextLevel;
                public readonly SurvivorNetworkWeaponUpgradeOption[] Options;

                public LeveledUp(string userId, int level, int experience, int experienceToNextLevel,
                    SurvivorNetworkWeaponUpgradeOption[] options)
                {
                    UserId = userId;
                    Level = level;
                    Experience = experience;
                    ExperienceToNextLevel = experienceToNextLevel;
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

            /// <summary>仮死状態からの復活通知 (PR4 では受け皿のみ、発火経路は将来 PR で実装)</summary>
            public readonly struct Revived
            {
                public readonly string UserId;

                public Revived(string userId)
                {
                    UserId = userId;
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
                public readonly int Count;

                public BatchUpdated(SurvivorNetworkEnemyStateSnapshot[] enemies, int count)
                {
                    Enemies = enemies;
                    Count = count;
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
            /// Server: WaveClearScore=0（消費者がローカル計算）
            /// Client: WaveClearScore=サーバー計算済み値
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
                public readonly int NetworkId;
                public readonly int ItemId;
                public readonly float PosX;
                public readonly float PosY;
                public readonly float PosZ;

                public Spawned(int networkId, int itemId, float posX, float posY, float posZ)
                {
                    NetworkId = networkId;
                    ItemId = itemId;
                    PosX = posX;
                    PosY = posY;
                    PosZ = posZ;
                }
            }

            public readonly struct Despawned
            {
                public readonly int NetworkId;

                public Despawned(int networkId)
                {
                    NetworkId = networkId;
                }
            }

            /// <summary>クライアント→サーバー: アイテム収集報告</summary>
            public readonly struct CollectReported
            {
                public readonly string UserId;
                public readonly int NetworkId;

                public CollectReported(string userId, int networkId)
                {
                    UserId = userId;
                    NetworkId = networkId;
                }
            }
        }

        // --- Weapon ---

        public static class Weapon
        {
            public readonly struct HitReported
            {
                public readonly string UserId;
                public readonly int EnemyNetworkId;
                public readonly int WeaponId;

                public HitReported(string userId, int enemyNetworkId, int weaponId)
                {
                    UserId = userId;
                    EnemyNetworkId = enemyNetworkId;
                    WeaponId = weaponId;
                }
            }

            public readonly struct ApplyRequested
            {
                public readonly SurvivorWeaponApplyRequest Request;

                public ApplyRequested(SurvivorWeaponApplyRequest request)
                {
                    Request = request;
                }
            }
        }
    }
}
