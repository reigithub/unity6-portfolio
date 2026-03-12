using System.Collections.Generic;
using Fusion;
using Game.Shared.Bootstrap;
using Game.Shared.Network.Survivor;
using Game.Shared.Signals.Survivor;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Shared.Network.Fusion
{
    /// <summary>
    /// Fusion 2 ゲーム状態シングルトン NetworkBehaviour。
    /// 旧 SurvivorNetworkGameManager に相当。
    /// サーバー側でゲームイベントを発生させ、Fusion RPC 経由で全クライアントへブロードキャスト。
    /// クライアント側では MessagePipe IPublisher 経由で既存の UI コードへ配信する。
    /// </summary>
    public class SurvivorFusionGameState : NetworkBehaviour
    {
        public static SurvivorFusionGameState Instance { get; private set; }

        // --- MessagePipe Publishers (VContainer InjectGameObject で解決) ---

        [Inject] private IPublisher<SurvivorSignals.Player.DamageReceived> _playerDamagedPub;
        [Inject] private IPublisher<SurvivorSignals.Player.Died> _playerDiedPub;
        [Inject] private IPublisher<SurvivorSignals.Player.ItemCollected> _itemCollectedPub;
        [Inject] private IPublisher<SurvivorSignals.Player.LeveledUp> _playerLeveledUpPub;
        [Inject] private IPublisher<SurvivorSignals.Player.WeaponChanged> _weaponChangedPub;
        [Inject] private IPublisher<SurvivorSignals.Enemy.Killed> _enemyKilledPub;
        [Inject] private IPublisher<SurvivorSignals.Wave.Started> _waveStartedPub;
        [Inject] private IPublisher<SurvivorSignals.Wave.Completed> _waveClearedPub;
        [Inject] private IPublisher<SurvivorSignals.Wave.AllCleared> _allWavesClearedPub;
        [Inject] private IPublisher<SurvivorSignals.Wave.TimeUp> _timeUpPub;
        [Inject] private IPublisher<SurvivorSignals.Game.Ended> _gameEndedPub;
        [Inject] private IPublisher<SurvivorSignals.Game.Paused> _gamePausedPub;
        [Inject] private IPublisher<SurvivorSignals.Game.Resumed> _gameResumedPub;
        [Inject] private IPublisher<SurvivorSignals.Connection.PlayerConnected> _playerConnectedPub;
        [Inject] private IPublisher<SurvivorSignals.Connection.PlayerDisconnected> _playerDisconnectedPub;
        [Inject] private IPublisher<SurvivorSignals.Weapon.HitReported> _hitReportedPub;
        [Inject] private IPublisher<SurvivorSignals.Weapon.ApplyRequested> _weaponApplyPub;
        [Inject] private IPublisher<SurvivorSignals.Session.AllClientsSceneReady> _allClientsSceneReadyPub;
        [Inject] private IPublisher<SurvivorSignals.Item.Spawned> _itemSpawnedPub;
        [Inject] private IPublisher<SurvivorSignals.Item.Despawned> _itemDespawnedPub;

        // --- [Networked] 永続状態（遅延参加クライアント用） ---

        [Networked] public int CurrentWave { get; set; }
        [Networked] public int WaveTargetKills { get; set; }
        [Networked] public int WaveTotalEnemies { get; set; }
        [Networked] public NetworkBool IsPaused { get; set; }
        [Networked] public NetworkBool IsAllWavesCleared { get; set; }

        // --- ChangeDetector（遅延参加クライアント向け状態同期） ---

        private ChangeDetector _changeDetector;

        // --- サーバー側状態 ---

        private readonly HashSet<string> _deadPlayerIds = new();
        private int _totalPlayerCount;
        private bool _isLevelUpPaused;
        private float _levelUpPauseStartTime;
        private const float LevelUpPauseTimeout = 45f;
        private SurvivorNetworkWeaponUpgradeOption[] _lastSentWeaponOptions;
        private readonly HashSet<PlayerRef> _sceneReadyPlayers = new();

        // =====================================================================
        //  ライフサイクル
        // =====================================================================

        public override void Spawned()
        {
            // StateAuthority インスタンスを優先（SP モードで Client レプリカに上書きされるのを防ぐ）
            if (HasStateAuthority || Instance == null)
            {
                Instance = this;
            }
            DontDestroyOnLoad(gameObject);

            // クライアント側レプリカ: onBeforeSpawned が実行されないため、Runner 経由で DI 注入
            if (_waveStartedPub == null)
            {
                var fusionRunner = Runner.GetComponent<SurvivorFusionRunner>();
                fusionRunner?.Resolver?.InjectGameObject(gameObject);
            }

            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
            Debug.Log($"[SurvivorFusionGameState] Spawned (StateAuth={HasStateAuthority}, DI={_waveStartedPub != null}, IsInstance={Instance == this})");
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!HasStateAuthority || !_isLevelUpPaused) return;

            if (Time.realtimeSinceStartup - _levelUpPauseStartTime > LevelUpPauseTimeout)
            {
                Debug.LogWarning("[SurvivorFusionGameState] LevelUp pause timeout, force resuming");
                _isLevelUpPaused = false;
                ApplicationEvents.ResumeTime();
            }
        }

        /// <summary>
        /// ChangeDetector → MessagePipe ブリッジ。
        /// 遅延参加クライアントが [Networked] 状態の最新値を受け取るために使用。
        /// </summary>
        public override void Render()
        {
            if (_changeDetector == null) return;

            foreach (var change in _changeDetector.DetectChanges(this))
            {
                switch (change)
                {
                    case nameof(CurrentWave):
                        Debug.Log($"[SurvivorFusionGameState] ChangeDetector: CurrentWave={CurrentWave}");
                        _waveStartedPub?.Publish(
                            new SurvivorSignals.Wave.Started(CurrentWave, WaveTargetKills, WaveTotalEnemies));
                        break;
                    case nameof(IsPaused):
                        Debug.Log($"[SurvivorFusionGameState] ChangeDetector: IsPaused={IsPaused}");
                        if (IsPaused)
                            _gamePausedPub?.Publish(new SurvivorSignals.Game.Paused(""));
                        else
                            _gameResumedPub?.Publish(new SurvivorSignals.Game.Resumed());
                        break;
                    case nameof(IsAllWavesCleared):
                        Debug.Log($"[SurvivorFusionGameState] ChangeDetector: IsAllWavesCleared={IsAllWavesCleared}");
                        if (IsAllWavesCleared)
                            _allWavesClearedPub?.Publish(new SurvivorSignals.Wave.AllCleared());
                        break;
                }
            }
        }

        // =====================================================================
        //  ウェーブイベント
        // =====================================================================

        /// <summary>サーバー側: ウェーブ開始を全クライアントに通知</summary>
        public void NotifyWaveStarted(int waveNumber, int targetKills, int totalEnemies)
        {
            if (!HasStateAuthority) return;
            CurrentWave = waveNumber;
            WaveTargetKills = targetKills;
            WaveTotalEnemies = totalEnemies;
            RpcNotifyWaveStarted(waveNumber, targetKills, totalEnemies);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcNotifyWaveStarted(int waveNumber, int targetKills, int totalEnemies)
        {
            Debug.Log($"[SurvivorFusionGameState] WaveStarted: wave={waveNumber}");
            _waveStartedPub?.Publish(
                new SurvivorSignals.Wave.Started(waveNumber, targetKills, totalEnemies));
        }

        /// <summary>サーバー側: ウェーブクリアを全クライアントに通知</summary>
        public void NotifyWaveCompleted(int waveNumber, int nextWaveNumber, int waveClearScore)
        {
            if (!HasStateAuthority) return;
            RpcNotifyWaveCompleted(waveNumber, waveClearScore);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcNotifyWaveCompleted(int waveNumber, int waveClearScore)
        {
            Debug.Log($"[SurvivorFusionGameState] WaveCompleted: wave={waveNumber}");
            _waveClearedPub?.Publish(
                new SurvivorSignals.Wave.Completed(waveNumber, waveClearScore));
        }

        /// <summary>サーバー側: 全ウェーブクリアを通知</summary>
        public void NotifyAllWavesCleared()
        {
            if (!HasStateAuthority) return;
            IsAllWavesCleared = true;
            RpcNotifyAllWavesCleared();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcNotifyAllWavesCleared()
        {
            Debug.Log("[SurvivorFusionGameState] AllWavesCleared");
            _allWavesClearedPub?.Publish(new SurvivorSignals.Wave.AllCleared());
        }

        /// <summary>サーバー側: 制限時間超過を通知</summary>
        public void NotifyTimeUp()
        {
            if (!HasStateAuthority) return;
            RpcNotifyTimeUp();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcNotifyTimeUp()
        {
            _timeUpPub?.Publish(new SurvivorSignals.Wave.TimeUp());
        }

        // =====================================================================
        //  プレイヤーイベント
        // =====================================================================

        /// <summary>サーバー側: プレイヤーダメージを通知</summary>
        public void NotifyPlayerDamaged(int damage, int currentHp)
        {
            if (!HasStateAuthority) return;
            RpcNotifyPlayerDamaged(damage, currentHp);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcNotifyPlayerDamaged(int damage, int currentHp)
        {
            _playerDamagedPub?.Publish(
                new SurvivorSignals.Player.DamageReceived(damage, currentHp));
        }

        /// <summary>サーバー側: プレイヤー死亡を通知</summary>
        public void NotifyPlayerDied()
        {
            if (!HasStateAuthority) return;
            RpcNotifyPlayerDied();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcNotifyPlayerDied()
        {
            Debug.Log("[SurvivorFusionGameState] PlayerDied");
            _playerDiedPub?.Publish(new SurvivorSignals.Player.Died());
        }

        /// <summary>サーバー側: アイテム収集を通知</summary>
        public void NotifyItemCollected(string userId, int itemId, int itemType, int effectValue,
            int currentExperience, int experienceToNextLevel)
        {
            if (!HasStateAuthority) return;
            RpcNotifyItemCollected(userId, itemId, itemType, effectValue,
                currentExperience, experienceToNextLevel);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcNotifyItemCollected(NetworkString<_64> userId, int itemId, int itemType, int effectValue,
            int currentExperience, int experienceToNextLevel)
        {
            _itemCollectedPub?.Publish(
                new SurvivorSignals.Player.ItemCollected(
                    userId.ToString(), itemId, itemType, effectValue,
                    currentExperience, experienceToNextLevel));
        }

        /// <summary>サーバー側: レベルアップを通知（武器選択肢付き）</summary>
        public void NotifyPlayerLevelUp(string userId, int newLevel,
            int experience, int experienceToNextLevel,
            SurvivorNetworkWeaponUpgradeOption[] options)
        {
            if (!HasStateAuthority) return;
            // 武器選択肢はサーバー側で記録（検証用）
            _lastSentWeaponOptions = options;
            // Host モード: 直接 Publish（RPC 経由だと配列が渡せないため）
            _playerLeveledUpPub?.Publish(
                new SurvivorSignals.Player.LeveledUp(
                    userId, newLevel, experience, experienceToNextLevel, options));
        }

        /// <summary>サーバー側: 武器変更を通知</summary>
        public void NotifyWeaponChanged(string userId, int weaponId, int level, bool isNew)
        {
            if (!HasStateAuthority) return;
            RpcNotifyWeaponChanged(userId, weaponId, level, isNew);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcNotifyWeaponChanged(NetworkString<_64> userId, int weaponId, int level, NetworkBool isNew)
        {
            _weaponChangedPub?.Publish(
                new SurvivorSignals.Player.WeaponChanged(userId.ToString(), weaponId, level, isNew));
        }

        // =====================================================================
        //  敵・スコア
        // =====================================================================

        /// <summary>サーバー側: 敵撃破を通知</summary>
        public void NotifyEnemyKilled(string killerUserId, int enemyId, int scoreGained, int totalKills)
        {
            if (!HasStateAuthority) return;
            RpcNotifyEnemyKilled(killerUserId, enemyId, scoreGained, totalKills);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcNotifyEnemyKilled(NetworkString<_64> killerUserId, int enemyId, int scoreGained, int totalKills)
        {
            _enemyKilledPub?.Publish(
                new SurvivorSignals.Enemy.Killed(killerUserId.ToString(), enemyId, scoreGained, totalKills));
        }

        // =====================================================================
        //  ゲーム終了
        // =====================================================================

        /// <summary>サーバー側: ゲーム終了を通知</summary>
        public void NotifyGameEnded(bool isVictory, float clearTime)
        {
            if (!HasStateAuthority) return;
            RpcNotifyGameEnded(isVictory, clearTime);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcNotifyGameEnded(NetworkBool isVictory, float clearTime)
        {
            Debug.Log($"[SurvivorFusionGameState] GameEnded: victory={isVictory}");
            var result = new SurvivorNetworkGameResult
            {
                IsVictory = isVictory,
                ClearTime = clearTime
            };
            _gameEndedPub?.Publish(new SurvivorSignals.Game.Ended(result));
        }

        // =====================================================================
        //  ポーズ
        // =====================================================================

        /// <summary>サーバー側: ゲームポーズを通知</summary>
        public void NotifyGamePaused(string requestedByUserId)
        {
            if (!HasStateAuthority) return;
            IsPaused = true;
            RpcNotifyGamePaused(requestedByUserId);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcNotifyGamePaused(NetworkString<_64> requestedByUserId)
        {
            _gamePausedPub?.Publish(
                new SurvivorSignals.Game.Paused(requestedByUserId.ToString()));
        }

        /// <summary>サーバー側: ゲーム再開を通知</summary>
        public void NotifyGameResumed()
        {
            if (!HasStateAuthority) return;
            IsPaused = false;
            RpcNotifyGameResumed();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcNotifyGameResumed()
        {
            _gameResumedPub?.Publish(new SurvivorSignals.Game.Resumed());
        }

        // =====================================================================
        //  接続
        // =====================================================================

        /// <summary>サーバー側: プレイヤー接続を通知</summary>
        public void NotifyPlayerConnected(string userId, string playerName)
        {
            if (!HasStateAuthority) return;
            RpcNotifyPlayerConnected(userId, playerName);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcNotifyPlayerConnected(NetworkString<_64> userId, NetworkString<_64> playerName)
        {
            _playerConnectedPub?.Publish(
                new SurvivorSignals.Connection.PlayerConnected(userId.ToString(), playerName.ToString()));
        }

        /// <summary>サーバー側: プレイヤー切断を通知</summary>
        public void NotifyPlayerDisconnected(string userId, string playerName)
        {
            if (!HasStateAuthority) return;
            RpcNotifyPlayerDisconnected(userId, playerName);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcNotifyPlayerDisconnected(NetworkString<_64> userId, NetworkString<_64> playerName)
        {
            _playerDisconnectedPub?.Publish(
                new SurvivorSignals.Connection.PlayerDisconnected(userId.ToString(), playerName.ToString()));
        }

        // =====================================================================
        //  アイテム同期（RPC 経由で全クライアントに配信）
        // =====================================================================

        /// <summary>サーバー側: アイテムスポーンを全クライアントに通知</summary>
        public void NotifyItemSpawned(int itemId, float posX, float posY, float posZ)
        {
            if (!HasStateAuthority) return;
            RpcNotifyItemSpawned(itemId, posX, posY, posZ);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcNotifyItemSpawned(int itemId, float posX, float posY, float posZ)
        {
            Debug.Log($"[SurvivorFusionGameState] RpcItemSpawned: id={itemId}");
            _itemSpawnedPub?.Publish(new SurvivorSignals.Item.Spawned(itemId, posX, posY, posZ));
        }

        /// <summary>サーバー側: アイテムデスポーンを全クライアントに通知</summary>
        public void NotifyItemDespawned(int itemId)
        {
            if (!HasStateAuthority) return;
            RpcNotifyItemDespawned(itemId);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcNotifyItemDespawned(int itemId)
        {
            Debug.Log($"[SurvivorFusionGameState] RpcItemDespawned: id={itemId}");
            _itemDespawnedPub?.Publish(new SurvivorSignals.Item.Despawned(itemId));
        }

        // =====================================================================
        //  サーバー側ロジック: 全滅判定
        // =====================================================================

        /// <summary>期待プレイヤー数を設定する。全滅判定の分母として使用。</summary>
        public void SetTotalPlayerCount(int count)
        {
            _totalPlayerCount = count;
            _deadPlayerIds.Clear();
        }

        /// <summary>
        /// プレイヤー死亡を記録する。全プレイヤーが死亡した場合、GameOver を通知。
        /// </summary>
        public void OnPlayerDied(string userId)
        {
            _deadPlayerIds.Add(userId);
            Debug.Log($"[SurvivorFusionGameState] Player died: {userId} ({_deadPlayerIds.Count}/{_totalPlayerCount})");

            if (_totalPlayerCount > 0 && _deadPlayerIds.Count >= _totalPlayerCount)
            {
                Debug.Log("[SurvivorFusionGameState] All players dead, sending GameOver");
                NotifyGameEnded(false, Time.time);
            }
        }

        // =====================================================================
        //  サーバー側ロジック: レベルアップポーズ管理
        // =====================================================================

        /// <summary>サーバー側: レベルアップポーズ要求</summary>
        public void OnClientRequestPause()
        {
            if (_isLevelUpPaused) return;
            _isLevelUpPaused = true;
            _levelUpPauseStartTime = Time.realtimeSinceStartup;
            ApplicationEvents.PauseTime();
            Debug.Log("[SurvivorFusionGameState] LevelUp pause requested");
        }

        /// <summary>サーバー側: レベルアップ再開要求</summary>
        public void OnClientRequestResume()
        {
            if (!_isLevelUpPaused) return;
            _isLevelUpPaused = false;
            ApplicationEvents.ResumeTime();
            Debug.Log("[SurvivorFusionGameState] LevelUp resumed");
        }

        // =====================================================================
        //  サーバー側ロジック: 武器選択検証
        // =====================================================================

        /// <summary>サーバー側: 送信した武器選択肢を記録（検証用）</summary>
        public void SetPendingWeaponOptions(SurvivorNetworkWeaponUpgradeOption[] options)
        {
            _lastSentWeaponOptions = options;
        }

        /// <summary>サーバー側: クライアントからの武器選択を検証・適用</summary>
        public void OnClientWeaponChoice(int weaponId, bool isNewWeapon)
        {
            if (_lastSentWeaponOptions != null)
            {
                bool valid = false;
                foreach (var opt in _lastSentWeaponOptions)
                {
                    if (opt.WeaponId == weaponId)
                    {
                        valid = true;
                        break;
                    }
                }
                if (!valid)
                {
                    Debug.LogWarning($"[SurvivorFusionGameState] Rejected invalid weapon choice: {weaponId}");
                    return;
                }
                _lastSentWeaponOptions = null;
            }

            var request = new WeaponApplyRequest
            {
                WeaponId = weaponId,
                IsNewWeapon = isNewWeapon,
                Type = WeaponApplyType.AddOrUpgrade
            };
            _weaponApplyPub?.Publish(new SurvivorSignals.Weapon.ApplyRequested(request));
        }

        /// <summary>サーバー側: クライアントからの武器入れ替えを適用</summary>
        public void OnClientWeaponReplace(int removeWeaponId, int newWeaponId)
        {
            var request = new WeaponApplyRequest
            {
                WeaponId = newWeaponId,
                RemoveWeaponId = removeWeaponId,
                Type = WeaponApplyType.Replace
            };
            _weaponApplyPub?.Publish(new SurvivorSignals.Weapon.ApplyRequested(request));
        }

        /// <summary>サーバー側: クライアントからのヒット報告</summary>
        public void OnClientHitReported(int enemyNetworkId, int weaponId)
        {
            _hitReportedPub?.Publish(new SurvivorSignals.Weapon.HitReported(enemyNetworkId, weaponId));
        }

        // =====================================================================
        //  サーバー側ロジック: シーン準備完了トラッキング
        // =====================================================================

        /// <summary>クライアントがシーン準備完了を通知</summary>
        public void OnClientSceneReady(PlayerRef player)
        {
            _sceneReadyPlayers.Add(player);
            Debug.Log($"[SurvivorFusionGameState] Client scene ready: {player} ({_sceneReadyPlayers.Count}/{_totalPlayerCount})");

            if (_totalPlayerCount > 0 && _sceneReadyPlayers.Count >= _totalPlayerCount)
            {
                Debug.Log("[SurvivorFusionGameState] All clients scene ready!");
                _allClientsSceneReadyPub?.Publish(new SurvivorSignals.Session.AllClientsSceneReady());
            }
        }

        /// <summary>セッション開始時にリセット</summary>
        public void ResetSceneReadyTracking()
        {
            _sceneReadyPlayers.Clear();
        }
    }
}
