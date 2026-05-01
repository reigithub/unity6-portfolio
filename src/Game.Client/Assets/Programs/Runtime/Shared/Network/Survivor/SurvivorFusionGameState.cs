using System.Collections.Generic;
using Fusion;
using Game.Shared.Network.Fusion;
using Game.Shared.Signals.Survivor;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// Fusion 2 ゲーム状態シングルトン NetworkBehaviour。
    /// 旧 SurvivorNetworkGameManager に相当。
    /// サーバー側でゲームイベントを発生させ、Fusion RPC 経由で全クライアントへブロードキャスト。
    /// クライアント側では MessagePipe IPublisher 経由で既存の UI コードへ配信する。
    /// </summary>
    public class SurvivorFusionGameState : NetworkBehaviour
    {
        [Inject] private IFusionRunnerService _runnerService;

        // --- MessagePipe Publishers ---
        // VContainer InjectGameObject で解決される。
        // サーバー専用 Publisher はクライアントDIスコープで null になるため、
        // 各 RPC 内で null 条件演算子（?.）でガードしている。

        // Player
        [Inject] private IPublisher<SurvivorSignals.Player.DamageReceived> _playerDamagedPub;
        [Inject] private IPublisher<SurvivorSignals.Player.Died> _playerDiedPub;
        [Inject] private IPublisher<SurvivorSignals.Player.ItemCollected> _itemCollectedPub;
        [Inject] private IPublisher<SurvivorSignals.Player.LeveledUp> _playerLeveledUpPub;
        [Inject] private IPublisher<SurvivorSignals.Player.WeaponChanged> _weaponChangedPub;

        // Enemy / Wave / Game
        [Inject] private IPublisher<SurvivorSignals.Enemy.Killed> _enemyKilledPub;
        [Inject] private IPublisher<SurvivorSignals.Wave.Started> _waveStartedPub;
        [Inject] private IPublisher<SurvivorSignals.Wave.Completed> _waveClearedPub;
        [Inject] private IPublisher<SurvivorSignals.Wave.AllCleared> _allWavesClearedPub;
        [Inject] private IPublisher<SurvivorSignals.Wave.TimeUp> _timeUpPub;
        [Inject] private IPublisher<SurvivorSignals.Game.Ended> _gameEndedPub;
        [Inject] private IPublisher<SurvivorSignals.Game.Paused> _gamePausedPub;
        [Inject] private IPublisher<SurvivorSignals.Game.Resumed> _gameResumedPub;

        // Connection / Session
        [Inject] private IPublisher<SurvivorSignals.Connection.PlayerConnected> _playerConnectedPub;
        [Inject] private IPublisher<SurvivorSignals.Connection.PlayerDisconnected> _playerDisconnectedPub;
        [Inject] private IPublisher<SurvivorSignals.Session.AllPlayersReady> _allPlayersReadyPub;
        [Inject] private IPublisher<SurvivorSignals.Session.AllClientsSceneReady> _allClientsSceneReadyPub;
        [Inject] private IPublisher<SurvivorSignals.Session.ClientFieldSceneLoaded> _clientFieldSceneLoadedPub;
        [Inject] private IPublisher<SurvivorSignals.Session.AllClientsFieldSceneLoaded> _allClientsFieldSceneLoadedPub;

        // Weapon / Item（サーバー側: クライアントRPCからのイベント中継）
        [Inject] private IPublisher<SurvivorSignals.Weapon.HitReported> _hitReportedPub;
        [Inject] private IPublisher<SurvivorSignals.Weapon.ApplyRequested> _weaponApplyPub;
        [Inject] private IPublisher<SurvivorSignals.Item.Spawned> _itemSpawnedPub;
        [Inject] private IPublisher<SurvivorSignals.Item.Despawned> _itemDespawnedPub;
        [Inject] private IPublisher<SurvivorSignals.Item.CollectReported> _itemCollectReportedPub;

        // --- [Networked] 永続状態（遅延参加クライアント用） ---

        [Networked] public int CurrentWave { get; set; }
        [Networked] public int WaveTargetKills { get; set; }
        [Networked] public int WaveTotalEnemies { get; set; }
        [Networked] public NetworkBool IsPaused { get; set; }

        /// <summary>Despawn後も安全にアクセス可能なポーズ状態。Object未生存時はfalseを返す。</summary>
        public bool IsEffectivelyPaused => Object != null && Object.IsValid && IsPaused;

        [Networked] public NetworkBool IsAllWavesCleared { get; set; }
        [Networked] public int StageId { get; set; }
        [Networked] public int PlayerId { get; set; }

        // --- ChangeDetector（遅延参加クライアント向け状態同期） ---

        private ChangeDetector _changeDetector;

        // --- サーバー側状態 ---

        private readonly HashSet<string> _deadPlayerIds = new();
        private readonly Dictionary<PlayerRef, string> _userIdByPlayerRef = new();
        private int _totalPlayerCount;
        private readonly HashSet<PlayerRef> _levelUpPausingPlayers = new();
        private bool _isManualPaused;
        private float _levelUpPauseStartTime;
        private const float LevelUpPauseTimeout = 45f;
        private readonly HashSet<PlayerRef> _sceneReadyPlayers = new();
        private readonly HashSet<PlayerRef> _fieldSceneReadyPlayers = new();

        // =====================================================================
        //  ライフサイクル
        // =====================================================================

        public override void Spawned()
        {
            DontDestroyOnLoad(gameObject);

            _runnerService?.Register(this);
            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
            Debug.Log($"[SurvivorFusionGameState] Spawned (StateAuth={HasStateAuthority}, DI={_waveStartedPub != null})");
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _runnerService?.Unregister(this);
            Destroy(gameObject);
        }

        private void Update()
        {
            if (!HasStateAuthority || _levelUpPausingPlayers.Count == 0) return;

            if (Time.realtimeSinceStartup - _levelUpPauseStartTime > LevelUpPauseTimeout)
            {
                Debug.LogWarning("[SurvivorFusionGameState] LevelUp pause timeout, force clearing");
                _levelUpPausingPlayers.Clear();
                RecomputeIsPaused();
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

        /// <summary>
        /// サーバー側: ウェーブ開始を全クライアントに通知。
        /// [Networked] プロパティを直接更新し、ChangeDetector (Render) で検知・Publish する方式。
        /// 遅延参加クライアントが CurrentWave/WaveTargetKills/WaveTotalEnemies を即座に取得できるため、
        /// 状態同期が必要な情報はこのパターンを使用する。
        /// </summary>
        public void NotifyWaveStarted(int waveNumber, int targetKills, int totalEnemies)
        {
            if (!HasStateAuthority) return;
            CurrentWave = waveNumber;
            WaveTargetKills = targetKills;
            WaveTotalEnemies = totalEnemies;
            // ChangeDetector (Render) が CurrentWave 変更を検知して _waveStartedPub に Publish
        }

        /// <summary>
        /// サーバー側: ウェーブクリアを全クライアントに通知。
        /// RPC で明示的にブロードキャストする方式。
        /// クリアスコアなど一時的なイベントデータ（状態として保持不要）はこのパターンを使用する。
        /// </summary>
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
            // ChangeDetector (Render) が IsAllWavesCleared 変更を検知して _allWavesClearedPub に Publish
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
        public void NotifyPlayerDamaged(PlayerRef target, int damage, int currentHp)
        {
            if (!HasStateAuthority) return;
            string userId = TryGetUserId(target, out var uid) ? uid : string.Empty;
            RpcNotifyPlayerDamaged(userId, damage, currentHp);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcNotifyPlayerDamaged(NetworkString<_64> userId, int damage, int currentHp)
        {
            _playerDamagedPub?.Publish(
                new SurvivorSignals.Player.DamageReceived(userId.ToString(), damage, currentHp));
        }

        /// <summary>サーバー側: プレイヤー死亡を通知</summary>
        public void NotifyPlayerDied(PlayerRef target)
        {
            if (!HasStateAuthority) return;
            string userId = TryGetUserId(target, out var uid) ? uid : string.Empty;
            RpcNotifyPlayerDied(userId);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcNotifyPlayerDied(NetworkString<_64> userId)
        {
            Debug.Log($"[SurvivorFusionGameState] PlayerDied: {userId}");
            _playerDiedPub?.Publish(new SurvivorSignals.Player.Died(userId.ToString()));
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
        public void NotifyGameEnded(bool isVictory, float clearTime, int totalKills = 0)
        {
            if (!HasStateAuthority) return;
            RpcNotifyGameEnded(isVictory, clearTime, totalKills);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcNotifyGameEnded(NetworkBool isVictory, float clearTime, int totalKills)
        {
            Debug.Log($"[SurvivorFusionGameState] GameEnded: victory={isVictory}, totalKills={totalKills}");
            var result = new SurvivorNetworkGameResult
            {
                IsVictory = isVictory,
                ClearTime = clearTime,
                TotalKills = totalKills,
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
            // ChangeDetector (Render) が IsPaused=true を検知して _gamePausedPub に Publish
        }

        /// <summary>サーバー側: ゲーム再開を通知</summary>
        public void NotifyGameResumed()
        {
            if (!HasStateAuthority) return;
            IsPaused = false;
            // ChangeDetector (Render) が IsPaused=false を検知して _gameResumedPub に Publish
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

        /// <summary>サーバー側: アイテムスポーンを全クライアントに通知（networkId で個体識別）</summary>
        public void NotifyItemSpawned(int networkId, int itemId, float posX, float posY, float posZ)
        {
            if (!HasStateAuthority) return;
            RpcNotifyItemSpawned(networkId, itemId, posX, posY, posZ);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcNotifyItemSpawned(int networkId, int itemId, float posX, float posY, float posZ)
        {
            Debug.Log($"[SurvivorFusionGameState] RpcItemSpawned: nid={networkId}, id={itemId}");
            _itemSpawnedPub?.Publish(new SurvivorSignals.Item.Spawned(networkId, itemId, posX, posY, posZ));
        }

        /// <summary>サーバー側: アイテムデスポーンを全クライアントに通知（networkId で個体識別）</summary>
        public void NotifyItemDespawned(int networkId)
        {
            if (!HasStateAuthority) return;
            RpcNotifyItemDespawned(networkId);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcNotifyItemDespawned(int networkId)
        {
            Debug.Log($"[SurvivorFusionGameState] RpcItemDespawned: nid={networkId}");
            _itemDespawnedPub?.Publish(new SurvivorSignals.Item.Despawned(networkId));
        }

        // =====================================================================
        //  サーバー側ロジック: 全滅判定
        // =====================================================================

        // =====================================================================
        //  PlayerRef ↔ UserId マッピング
        // =====================================================================

        /// <summary>サーバー側: PlayerRef と UserId の対応を登録する</summary>
        public void RegisterPlayerUserId(PlayerRef player, string userId)
        {
            if (string.IsNullOrEmpty(userId)) return;
            _userIdByPlayerRef[player] = userId;
            Debug.Log($"[SurvivorFusionGameState] RegisterPlayerUserId: {player} → {userId}");
        }

        public bool TryGetUserId(PlayerRef player, out string userId)
        {
            return _userIdByPlayerRef.TryGetValue(player, out userId);
        }

        public bool TryGetPlayerRef(string userId, out PlayerRef player)
        {
            foreach (var kvp in _userIdByPlayerRef)
            {
                if (kvp.Value == userId)
                {
                    player = kvp.Key;
                    return true;
                }
            }
            player = default;
            return false;
        }

        /// <summary>クライアント→サーバー: 自分の UserId を登録する RPC。
        /// <para>
        /// GameState は singleton NetworkBehaviour で InputAuthority を持たない (PlayerRef.None) ため、
        /// <see cref="RpcSources.InputAuthority"/> を指定すると「Local simulation は送信不可」で拒否される。
        /// <see cref="RpcSources.All"/> にして任意クライアントから送信可能にし、<c>info.Source</c> で発信者 PlayerRef を取得する。
        /// </para>
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RpcRegisterPlayerUserId(NetworkString<_64> userId, RpcInfo info = default)
        {
            RegisterPlayerUserId(info.Source, userId.ToString());
        }

        /// <summary>期待プレイヤー数を設定する。全滅判定の分母として使用。</summary>
        public void SetTotalPlayerCount(int count)
        {
            _totalPlayerCount = count;
            _deadPlayerIds.Clear();
            _userIdByPlayerRef.Clear();
            _sceneReadyPlayers.Clear();
            _fieldSceneReadyPlayers.Clear();
            _levelUpPausingPlayers.Clear();
            _isManualPaused = false;

            // [Networked] ゲーム状態をリセット（リトライ時に ChangeDetector が正しく変化を検知するため）
            CurrentWave = 0;
            WaveTargetKills = 0;
            WaveTotalEnemies = 0;
            IsPaused = false;
            IsAllWavesCleared = false;
            StageId = 0;
            PlayerId = 0;
        }

        /// <summary>
        /// プレイヤー死亡を記録する。全プレイヤーが死亡した場合、GameOver を通知。
        /// </summary>
        public void OnPlayerDied(string userId)
        {
            if (!HasStateAuthority) return;
            if (!_deadPlayerIds.Add(userId))
            {
                Debug.LogWarning($"[SurvivorFusionGameState] Duplicate death for player: {userId}");
                return;
            }
            Debug.Log($"[SurvivorFusionGameState] Player died: {userId} ({_deadPlayerIds.Count}/{_totalPlayerCount})");

            if (_totalPlayerCount > 0 && _deadPlayerIds.Count >= _totalPlayerCount)
            {
                Debug.Log("[SurvivorFusionGameState] All players dead, sending GameOver");
                NotifyGameEnded(false, Time.time);
            }
        }

        /// <summary>PlayerRef オーバーロード: UserId を解決して既存ロジックに委譲する</summary>
        public void OnPlayerDied(PlayerRef source)
        {
            if (TryGetUserId(source, out var userId))
            {
                OnPlayerDied(userId);
            }
            else
            {
                Debug.LogWarning($"[SurvivorFusionGameState] OnPlayerDied: UserId not found for {source}");
            }
        }

        /// <summary>
        /// 将来の復活プロセス実装用フック (PR4 では受け皿のみ)。
        /// 呼ばれたプレイヤーを <see cref="_deadPlayerIds"/> から除外するだけで、
        /// 復活 RPC / トリガー (時間自動/蘇生行動) は将来 PR で実装する。
        /// </summary>
        public void OnPlayerRevived(string userId)
        {
            if (!HasStateAuthority) return;
            if (_deadPlayerIds.Remove(userId))
            {
                Debug.Log($"[SurvivorFusionGameState] Player revived (placeholder): {userId}");
            }
        }

        // =====================================================================
        //  サーバー側ロジック: ポーズ管理（参照カウント方式）
        //  IsPaused = (LevelUp 中の Player が 1 人以上) || ManualPause
        // =====================================================================

        /// <summary>
        /// LevelUp ポーズ開始（サーバー LevelUpState から即時呼出）。
        /// HashSet に追加し、IsPaused を再計算する。RPC 往復を待たないため遅延ゼロ。
        /// </summary>
        public void BeginLevelUpPause(PlayerRef player)
        {
            if (!HasStateAuthority) return;
            if (_levelUpPausingPlayers.Add(player))
            {
                if (_levelUpPausingPlayers.Count == 1)
                {
                    _levelUpPauseStartTime = Time.realtimeSinceStartup;
                }
                RecomputeIsPaused();
                Debug.Log($"[SurvivorFusionGameState] BeginLevelUpPause: {player} (count={_levelUpPausingPlayers.Count})");
            }
        }

        /// <summary>
        /// LevelUp ポーズ終了（武器選択受信時に呼ばれる）。
        /// HashSet から除去し、空かつマニュアル Pause も無ければ IsPaused=false。
        /// </summary>
        public void EndLevelUpPause(PlayerRef player)
        {
            if (!HasStateAuthority) return;
            if (_levelUpPausingPlayers.Remove(player))
            {
                RecomputeIsPaused();
                Debug.Log($"[SurvivorFusionGameState] EndLevelUpPause: {player} (count={_levelUpPausingPlayers.Count})");
            }
        }

        /// <summary>切断時のクリーンアップ。残留 LevelUp ポーズで全体停止が永続化するのを防ぐ。</summary>
        public void OnPlayerDisconnectedCleanup(PlayerRef player)
        {
            if (!HasStateAuthority) return;
            if (_levelUpPausingPlayers.Remove(player))
            {
                RecomputeIsPaused();
                Debug.Log($"[SurvivorFusionGameState] Cleanup on disconnect: {player} (count={_levelUpPausingPlayers.Count})");
            }
        }

        private void RecomputeIsPaused()
        {
            IsPaused = _levelUpPausingPlayers.Count > 0 || _isManualPaused;
        }

        /// <summary>サーバー側: マニュアルポーズ要求（ESC ダイアログ）</summary>
        public void OnClientRequestPause()
        {
            if (!HasStateAuthority || _isManualPaused) return;
            _isManualPaused = true;
            RecomputeIsPaused();
            Debug.Log("[SurvivorFusionGameState] Manual pause requested");
        }

        /// <summary>サーバー側: マニュアルポーズ解除</summary>
        public void OnClientRequestResume()
        {
            if (!HasStateAuthority || !_isManualPaused) return;
            _isManualPaused = false;
            RecomputeIsPaused();
            Debug.Log("[SurvivorFusionGameState] Manual pause released");
        }

        // =====================================================================
        //  サーバー側ロジック: 武器選択検証
        // =====================================================================

        /// <summary>サーバー側: クライアントからの武器選択を適用（検証は SurvivorFusionPlayer で実施済み）</summary>
        public void OnClientWeaponChoice(PlayerRef source, int weaponId, bool isNewWeapon)
        {
            EndLevelUpPause(source);

            var request = new SurvivorWeaponApplyRequest
            {
                WeaponId = weaponId,
                IsNewWeapon = isNewWeapon,
                Type = SurvivorWeaponApplyType.AddOrUpgrade
            };
            _weaponApplyPub?.Publish(new SurvivorSignals.Weapon.ApplyRequested(request));
        }

        /// <summary>サーバー側: クライアントからの武器入れ替えを適用</summary>
        public void OnClientWeaponReplace(int removeWeaponId, int newWeaponId)
        {
            var request = new SurvivorWeaponApplyRequest
            {
                WeaponId = newWeaponId,
                RemoveWeaponId = removeWeaponId,
                Type = SurvivorWeaponApplyType.Replace
            };
            _weaponApplyPub?.Publish(new SurvivorSignals.Weapon.ApplyRequested(request));
        }

        /// <summary>サーバー側: クライアントからのヒット報告（発信者 PlayerRef 経由）</summary>
        public void OnClientHitReported(PlayerRef source, int enemyNetworkId, int weaponId)
        {
            string userId = TryGetUserId(source, out var uid) ? uid : string.Empty;
            _hitReportedPub?.Publish(new SurvivorSignals.Weapon.HitReported(userId, enemyNetworkId, weaponId));
        }

        /// <summary>サーバー側: クライアントからのアイテム収集報告（networkId で個体識別、発信者 PlayerRef 経由）</summary>
        public void OnClientItemCollected(PlayerRef source, int networkId)
        {
            string userId = TryGetUserId(source, out var uid) ? uid : string.Empty;
            _itemCollectReportedPub?.Publish(new SurvivorSignals.Item.CollectReported(userId, networkId));
        }

        // =====================================================================
        //  サーバー側ロジック: シーン準備完了トラッキング
        // =====================================================================

        /// <summary>
        /// クライアント → サーバー: 選択したステージ ID とプレイヤー ID を通知。
        /// 最初に受信した値を採用（複数クライアント対応）。
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RpcSetSessionInfo(int stageId, int playerId)
        {
            if (StageId == 0)
            {
                StageId = stageId;
                PlayerId = playerId;
                Debug.Log($"[SurvivorFusionGameState] Session info set: stageId={stageId}, playerId={playerId}");
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcNotifyAllPlayersReady()
        {
            Debug.Log("[SurvivorFusionGameState] AllPlayersReady (RPC received)");
            _allPlayersReadyPub?.Publish(new SurvivorSignals.Session.AllPlayersReady());
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RpcNotifyFieldSceneLoaded(RpcInfo info = default)
        {
            var player = info.Source;
            _fieldSceneReadyPlayers.Add(player);
            Debug.Log($"[SurvivorFusionGameState] Client field scene loaded: {player} ({_fieldSceneReadyPlayers.Count}/{_totalPlayerCount})");

            _clientFieldSceneLoadedPub?.Publish(new SurvivorSignals.Session.ClientFieldSceneLoaded());

            if (_totalPlayerCount > 0 && _fieldSceneReadyPlayers.Count >= _totalPlayerCount)
            {
                Debug.Log("[SurvivorFusionGameState] All clients field scene loaded!");
                _allClientsFieldSceneLoadedPub?.Publish(new SurvivorSignals.Session.AllClientsFieldSceneLoaded());
            }
        }

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
