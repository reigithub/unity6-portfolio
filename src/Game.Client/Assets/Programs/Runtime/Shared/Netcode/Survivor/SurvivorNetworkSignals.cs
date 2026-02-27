namespace Game.Shared.Netcode.Survivor
{
    /// <summary>
    /// ネットワーク経由のゲームイベントシグナル定義。
    /// NetworkBehaviour(ClientRpc) → IPublisher → ISubscriber で配信される。
    /// SP 時は誰も Publish しないため、購読は無害。
    /// </summary>
    public static class SurvivorNetworkSignals
    {
        // --- セッション ---

        public readonly struct AllPlayersReady
        {
            public AllPlayersReady() { }
        }

        public readonly struct GameStarted
        {
            public readonly float ServerTime;

            public GameStarted(float serverTime)
            {
                ServerTime = serverTime;
            }
        }

        public readonly struct GameEnded
        {
            public readonly NetworkSurvivorGameResult Result;

            public GameEnded(NetworkSurvivorGameResult result)
            {
                Result = result;
            }
        }

        // --- 接続 ---

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

        // --- プレイヤー ---

        public readonly struct PlayerDamaged
        {
            public readonly string UserId;
            public readonly int Damage;
            public readonly int CurrentHp;

            public PlayerDamaged(string userId, int damage, int currentHp)
            {
                UserId = userId;
                Damage = damage;
                CurrentHp = currentHp;
            }
        }

        public readonly struct PlayerDied
        {
            public readonly string UserId;

            public PlayerDied(string userId)
            {
                UserId = userId;
            }
        }

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

        public readonly struct PlayerLeveledUp
        {
            public readonly string UserId;
            public readonly int Level;
            public readonly NetworkSurvivorWeaponUpgradeOption[] Options;

            public PlayerLeveledUp(string userId, int level, NetworkSurvivorWeaponUpgradeOption[] options)
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

        // --- 敵 ---

        public readonly struct EnemyKilled
        {
            public readonly string KillerUserId;
            public readonly int EnemyId;
            public readonly int ScoreGained;
            public readonly int TotalKills;

            public EnemyKilled(string killerUserId, int enemyId, int scoreGained, int totalKills)
            {
                KillerUserId = killerUserId;
                EnemyId = enemyId;
                ScoreGained = scoreGained;
                TotalKills = totalKills;
            }
        }

        // --- ウェーブ ---

        public readonly struct WaveStarted
        {
            public readonly int WaveNumber;
            public readonly int TargetKills;
            public readonly int TotalEnemies;

            public WaveStarted(int waveNumber, int targetKills, int totalEnemies)
            {
                WaveNumber = waveNumber;
                TargetKills = targetKills;
                TotalEnemies = totalEnemies;
            }
        }

        public readonly struct WaveCleared
        {
            public readonly int WaveNumber;
            public readonly int NextWaveNumber;
            public readonly int WaveClearScore;

            public WaveCleared(int waveNumber, int nextWaveNumber, int waveClearScore)
            {
                WaveNumber = waveNumber;
                NextWaveNumber = nextWaveNumber;
                WaveClearScore = waveClearScore;
            }
        }

        public readonly struct AllWavesCleared
        {
            public AllWavesCleared() { }
        }

        public readonly struct TimeUp
        {
            public TimeUp() { }
        }

        // --- ポーズ ---

        public readonly struct GamePaused
        {
            public readonly string RequestedByUserId;

            public GamePaused(string requestedByUserId)
            {
                RequestedByUserId = requestedByUserId;
            }
        }

        public readonly struct GameResumed
        {
            public GameResumed() { }
        }

        // --- バッチ同期 ---

        public readonly struct EnemyBatchUpdated
        {
            public readonly NetworkSurvivorEnemyStateSnapshot[] Enemies;

            public EnemyBatchUpdated(NetworkSurvivorEnemyStateSnapshot[] enemies)
            {
                Enemies = enemies;
            }
        }

        public readonly struct ItemSpawned
        {
            public readonly int ItemId;
            public readonly float PosX;
            public readonly float PosZ;

            public ItemSpawned(int itemId, float posX, float posZ)
            {
                ItemId = itemId;
                PosX = posX;
                PosZ = posZ;
            }
        }

        public readonly struct ItemDespawned
        {
            public readonly int ItemId;

            public ItemDespawned(int itemId)
            {
                ItemId = itemId;
            }
        }
    }
}
