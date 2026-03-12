using System;
using System.Collections.Generic;
using Game.Shared.Bootstrap;
using Game.Shared.Signals.Survivor;
using MessagePipe;
using Mirror;
using Unity.Collections;
using UnityEngine;
using VContainer;

namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// ゲーム全体のイベント配信 NetworkBehaviour（シングルトン）。
    /// サーバー側で発生したゲームイベントを ClientRpc 経由で全クライアントへブロードキャストし、
    /// クライアント側では MessagePipe の IPublisher 経由で対応するシグナルを配信する。
    /// <para>
    /// <b>呼び出し元:</b><br/>
    /// - <see cref="Game.Shared.Netcode.Server.SurvivorServerSession"/> (セッション管理)<br/>
    /// - SurvivorPlayerController.States (ダメージ・死亡)<br/>
    /// - SurvivorStageScene (ウェーブ進行)
    /// </para>
    /// </summary>
    public class SurvivorNetworkGameManager : NetworkBehaviour
    {
        public static SurvivorNetworkGameManager Instance { get; private set; }

        // --- IPublisher フィールド（VContainer InjectGameObject で解決） ---

        [Inject] private IPublisher<SurvivorSignals.Session.AllPlayersReady> _allPlayersReadyPub;
        [Inject] private IPublisher<SurvivorSignals.Session.GameStarted> _gameStartedPub;
        [Inject] private IPublisher<SurvivorSignals.Game.Ended> _gameEndedPub;
        [Inject] private IPublisher<SurvivorSignals.Connection.PlayerConnected> _playerConnectedPub;
        [Inject] private IPublisher<SurvivorSignals.Connection.PlayerDisconnected> _playerDisconnectedPub;
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
        [Inject] private IPublisher<SurvivorSignals.Game.Paused> _gamePausedPub;
        [Inject] private IPublisher<SurvivorSignals.Game.Resumed> _gameResumedPub;
        [Inject] private IPublisher<SurvivorSignals.Weapon.HitReported> _hitReportedPub;
        [Inject] private IPublisher<SurvivorSignals.Weapon.ApplyRequested> _weaponApplyPub;
        [Inject] private IPublisher<SurvivorSignals.Session.AllClientsSceneReady> _allClientsSceneReadyPub;

        // =====================================================================
        //  セッション
        // =====================================================================

        /// <summary>
        /// 全プレイヤーの接続が完了したことを通知する。
        /// <para><b>呼び出し元:</b> SurvivorServerSession.NotifyPlayersReadyAsync</para>
        /// </summary>
        [ClientRpc]
        public void NotifyAllPlayersReadyClientRpc()
        {
            Debug.Log("[NetworkSurvivorGameManager] AllPlayersReady");
            _allPlayersReadyPub?.Publish(new SurvivorSignals.Session.AllPlayersReady());
        }

        /// <summary>
        /// ゲーム開始をクライアントに通知する。サーバー時刻を基準に同期を取る。
        /// <para><b>呼び出し元:</b> SurvivorServerSession.NotifyPlayersReadyAsync</para>
        /// </summary>
        [ClientRpc]
        public void NotifyGameStartedClientRpc(float serverTime)
        {
            Debug.Log($"[NetworkSurvivorGameManager] GameStarted at serverTime={serverTime}");
            _gameStartedPub?.Publish(new SurvivorSignals.Session.GameStarted(serverTime));
        }

        // =====================================================================
        //  プレイヤーイベント
        // =====================================================================

        /// <summary>
        /// プレイヤーがダメージを受けたことを通知する。
        /// クライアント側では <see cref="IsLocalPlayer"/> でフィルタし、自分のダメージのみ StageModel へ反映する。
        /// <para><b>呼び出し元:</b> SurvivorPlayerController.States.TryProcessDamage (サーバー側 #if UNITY_SERVER)</para>
        /// </summary>
        [ClientRpc]
        public void NotifyPlayerDamagedClientRpc(FixedString64Bytes userId, int damage, int currentHp)
        {
            if (IsLocalPlayer(userId))
            {
                if (_playerDamagedPub == null)
                    Debug.LogWarning("[NetworkSurvivorGameManager] _playerDamagedPub is NULL");
                _playerDamagedPub?.Publish(
                    new SurvivorSignals.Player.DamageReceived(damage, currentHp));
            }
        }

        /// <summary>
        /// プレイヤーが死亡したことを通知する。
        /// クライアント側では <see cref="IsLocalPlayer"/> でフィルタし、自分の死亡のみ GameOver 遷移に反映する。
        /// <para><b>呼び出し元:</b> SurvivorPlayerController.States.DeadState.Enter (サーバー側 #if UNITY_SERVER)</para>
        /// </summary>
        [ClientRpc]
        public void NotifyPlayerDiedClientRpc(FixedString64Bytes userId)
        {
            Debug.Log($"[NetworkSurvivorGameManager] PlayerDied RPC received: userId={userId}");
            if (IsLocalPlayer(userId))
            {
                _playerDiedPub?.Publish(new SurvivorSignals.Player.Died());
            }
        }

        /// <summary>
        /// プレイヤーがアイテムを取得したことを通知する。
        /// <para><b>未使用:</b> アイテムシステムの MP 対応時に、サーバー側のアイテム取得ロジックから呼び出す予定。</para>
        /// </summary>
        [ClientRpc]
        public void NotifyItemCollectedClientRpc(
            FixedString64Bytes userId, int itemId, int itemType, int effectValue,
            int currentExperience, int experienceToNextLevel)
        {
            Debug.Log($"[NetworkSurvivorGameManager] ItemCollected RPC received: itemId={itemId}, type={itemType}, exp={currentExperience}");
            _itemCollectedPub?.Publish(
                new SurvivorSignals.Player.ItemCollected(
                    userId.ToString(), itemId, itemType, effectValue,
                    currentExperience, experienceToNextLevel));
        }

        /// <summary>
        /// プレイヤーがレベルアップし、武器アップグレード選択肢を提示することを通知する。
        /// <para><b>未使用:</b> レベルアップシステムの MP 対応時に、サーバー側の経験値計算から呼び出す予定。</para>
        /// </summary>
        [ClientRpc]
        public void NotifyPlayerLevelUpClientRpc(
            FixedString64Bytes userId, int newLevel,
            int experience, int experienceToNextLevel,
            SurvivorNetworkWeaponUpgradeOption[] options)
        {
            _playerLeveledUpPub?.Publish(
                new SurvivorSignals.Player.LeveledUp(
                    userId.ToString(), newLevel, experience, experienceToNextLevel, options));
        }

        /// <summary>
        /// プレイヤーの武器が変更（新規取得またはレベルアップ）されたことを通知する。
        /// <para><b>未使用:</b> 武器システムの MP 対応時に、サーバー側の武器変更処理から呼び出す予定。</para>
        /// </summary>
        [ClientRpc]
        public void NotifyWeaponChangedClientRpc(FixedString64Bytes userId, int weaponId, int level, bool isNew)
        {
            _weaponChangedPub?.Publish(
                new SurvivorSignals.Player.WeaponChanged(userId.ToString(), weaponId, level, isNew));
        }

        // =====================================================================
        //  敵・スコア
        // =====================================================================

        /// <summary>
        /// 敵が撃破されたことを通知する。キル数・スコアの同期に使用。
        /// <para><b>未使用:</b> スコアシステムの MP 対応時に、サーバー側の敵死亡処理から呼び出す予定。</para>
        /// </summary>
        [ClientRpc]
        public void NotifyEnemyKilledClientRpc(FixedString64Bytes killerUserId, int enemyId, int scoreGained, int totalKills)
        {
            _enemyKilledPub?.Publish(
                new SurvivorSignals.Enemy.Killed(killerUserId.ToString(), enemyId, scoreGained, totalKills));
        }

        // =====================================================================
        //  ウェーブ
        // =====================================================================

        /// <summary>
        /// ウェーブクリアを通知する。次ウェーブ番号とクリアスコアを含む。
        /// <para><b>呼び出し元:</b> SurvivorStageScene.SubscribeNetworkSignals (Wave.Completed シグナルブリッジ)</para>
        /// </summary>
        [ClientRpc]
        public void NotifyWaveClearedClientRpc(int waveNumber, int nextWaveNumber, int waveClearScore)
        {
            Debug.Log($"[NetworkSurvivorGameManager] WaveCleared RPC received: wave={waveNumber}, next={nextWaveNumber}");
            if (_waveClearedPub == null)
                Debug.LogWarning("[NetworkSurvivorGameManager] _waveClearedPub is NULL");
            _waveClearedPub?.Publish(
                new SurvivorSignals.Wave.Completed(waveNumber, waveClearScore));
        }

        /// <summary>
        /// 新ウェーブの開始を通知する。目標キル数と敵総数を含む。
        /// <para><b>呼び出し元:</b> SurvivorStageScene.SubscribeNetworkSignals (Wave.Started シグナルブリッジ)</para>
        /// </summary>
        [ClientRpc]
        public void NotifyWaveStartedClientRpc(int waveNumber, int targetKills, int totalEnemies)
        {
            Debug.Log($"[NetworkSurvivorGameManager] WaveStarted RPC received: wave={waveNumber}, target={targetKills}, enemies={totalEnemies}");
            if (_waveStartedPub == null)
                Debug.LogWarning("[NetworkSurvivorGameManager] _waveStartedPub is NULL — VContainer injection failed");
            _waveStartedPub?.Publish(
                new SurvivorSignals.Wave.Started(waveNumber, targetKills, totalEnemies));
        }

        /// <summary>
        /// 全ウェーブクリアを通知する。ゲームクリア判定に使用。
        /// <para><b>呼び出し元:</b> SurvivorStageScene.SubscribeNetworkSignals (Wave.AllCleared シグナルブリッジ)</para>
        /// </summary>
        [ClientRpc]
        public void NotifyAllWavesClearedClientRpc()
        {
            Debug.Log("[NetworkSurvivorGameManager] AllWavesCleared RPC received");
            _allWavesClearedPub?.Publish(new SurvivorSignals.Wave.AllCleared());
        }

        /// <summary>
        /// 制限時間超過を通知する。
        /// <para><b>未使用:</b> タイムアップ処理の MP 対応時に、サーバー側のタイマーから呼び出す予定。</para>
        /// </summary>
        [ClientRpc]
        public void NotifyTimeUpClientRpc()
        {
            _timeUpPub?.Publish(new SurvivorSignals.Wave.TimeUp());
        }

        // =====================================================================
        //  ゲーム終了
        // =====================================================================

        /// <summary>
        /// ゲーム終了を通知する。勝敗結果とクリアタイムを含む。
        /// <para><b>呼び出し元:</b>
        /// <see cref="OnPlayerDied"/> (全滅時) /
        /// SurvivorNetworkPlayerState (勝利時)</para>
        /// </summary>
        [ClientRpc]
        public void NotifyGameEndedClientRpc(SurvivorNetworkGameResult result)
        {
            Debug.Log($"[NetworkSurvivorGameManager] GameEnded RPC received: victory={result.IsVictory}");
            if (_gameEndedPub == null)
                Debug.LogWarning("[NetworkSurvivorGameManager] _gameEndedPub is NULL");
            _gameEndedPub?.Publish(new SurvivorSignals.Game.Ended(result));
        }

        // =====================================================================
        //  ポーズ
        // =====================================================================

        /// <summary>
        /// ゲームポーズを通知する。リクエストしたプレイヤーの userId を含む。
        /// <para><b>未使用:</b> ポーズシステムの MP 対応時に、サーバー側の RequestPauseServerRpc から呼び出す予定。</para>
        /// </summary>
        [ClientRpc]
        public void NotifyGamePausedClientRpc(FixedString64Bytes requestedByUserId)
        {
            _gamePausedPub?.Publish(
                new SurvivorSignals.Game.Paused(requestedByUserId.ToString()));
        }

        /// <summary>
        /// ゲーム再開を通知する。
        /// <para><b>未使用:</b> ポーズシステムの MP 対応時に、サーバー側の RequestResumeServerRpc から呼び出す予定。</para>
        /// </summary>
        [ClientRpc]
        public void NotifyGameResumedClientRpc()
        {
            _gameResumedPub?.Publish(new SurvivorSignals.Game.Resumed());
        }

        // =====================================================================
        //  レベルアップポーズ管理（サーバー側）
        // =====================================================================

        private bool _isLevelUpPaused;
        private float _levelUpPauseStartTime;
        private const float LevelUpPauseTimeout = 45f;

        // OnHitReported, OnWeaponApplyRequested → MessagePipe IPublisher に移行済み

        /// <summary>サーバー側: 最後に送信した武器選択肢（検証用）</summary>
        private SurvivorNetworkWeaponUpgradeOption[] _lastSentWeaponOptions;

        /// <summary>サーバー側: クライアントからのヒット報告を受信し、イベントを発火</summary>
        [Server]
        public void OnClientHitReported(int enemyNetworkId, int weaponId)
        {
            _hitReportedPub?.Publish(new SurvivorSignals.Weapon.HitReported(enemyNetworkId, weaponId));
        }

        /// <summary>サーバー側: 送信した武器選択肢を記録（検証用）</summary>
        public void SetPendingWeaponOptions(SurvivorNetworkWeaponUpgradeOption[] options)
        {
            _lastSentWeaponOptions = options;
        }

        /// <summary>サーバー側: クライアントがレベルアップポーズを要求</summary>
        [Server]
        public void OnClientRequestPause(NetworkConnectionToClient conn)
        {
            if (_isLevelUpPaused) return;
            _isLevelUpPaused = true;
            _levelUpPauseStartTime = Time.realtimeSinceStartup;
            ApplicationEvents.PauseTime();
            Debug.Log($"[NetworkSurvivorGameManager] LevelUp pause requested by conn={conn.connectionId}");
        }

        /// <summary>サーバー側: クライアントがレベルアップ再開を要求</summary>
        [Server]
        public void OnClientRequestResume(NetworkConnectionToClient conn)
        {
            if (!_isLevelUpPaused) return;
            _isLevelUpPaused = false;
            ApplicationEvents.ResumeTime();
            Debug.Log($"[NetworkSurvivorGameManager] LevelUp resumed by conn={conn.connectionId}");
        }

        /// <summary>サーバー側: 武器選択結果を受信し、検証後に適用イベントを発火</summary>
        [Server]
        public void OnClientWeaponChoice(int weaponId, bool isNewWeapon)
        {
            // 検証: サーバーが送った選択肢に含まれるか
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
                    Debug.LogWarning($"[NetworkSurvivorGameManager] Rejected invalid weapon choice: {weaponId}");
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

        /// <summary>サーバー側: 武器入れ替え結果を受信し、適用イベントを発火</summary>
        [Server]
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

        private void Update()
        {
            if (!isServer || !_isLevelUpPaused) return;

            if (Time.realtimeSinceStartup - _levelUpPauseStartTime > LevelUpPauseTimeout)
            {
                Debug.LogWarning("[NetworkSurvivorGameManager] LevelUp pause timeout, force resuming");
                _isLevelUpPaused = false;
                ApplicationEvents.ResumeTime();
            }
        }

        // =====================================================================
        //  接続
        // =====================================================================

        /// <summary>
        /// 新しいプレイヤーが接続したことを他クライアントに通知する。
        /// <para><b>未使用:</b> 接続通知 UI の実装時に、SurvivorServerSession.OnClientAuthenticated から呼び出す予定。</para>
        /// </summary>
        [ClientRpc]
        public void NotifyPlayerConnectedClientRpc(FixedString64Bytes userId, FixedString64Bytes playerName)
        {
            _playerConnectedPub?.Publish(
                new SurvivorSignals.Connection.PlayerConnected(userId.ToString(), playerName.ToString()));
        }

        /// <summary>
        /// プレイヤーが切断したことを残りクライアントに通知する。
        /// <para><b>呼び出し元:</b> SurvivorServerSession.OnClientDisconnected</para>
        /// </summary>
        [ClientRpc]
        public void NotifyPlayerDisconnectedClientRpc(FixedString64Bytes userId, FixedString64Bytes playerName)
        {
            _playerDisconnectedPub?.Publish(
                new SurvivorSignals.Connection.PlayerDisconnected(userId.ToString(), playerName.ToString()));
        }

        // =====================================================================
        //  ローカルプレイヤー判定（クライアント側フィルタリング用）
        // =====================================================================

        /// <summary>
        /// ClientRpc 受信時に、対象 userId がローカルプレイヤーかどうかを判定する。
        /// MP ではダメージ・死亡イベントを自プレイヤー分のみ StageModel に反映するために使用。
        /// SP モード (userId 空) はフォールバックで常に true を返す。
        /// </summary>
        private bool IsLocalPlayer(FixedString64Bytes userId)
        {
            var userIdStr = userId.ToString();
            if (string.IsNullOrEmpty(userIdStr)) return true;

            var localPlayer = NetworkClient.localPlayer;
            if (localPlayer == null) return true;

            var state = localPlayer.GetComponent<SurvivorNetworkPlayerState>();
            if (state == null) return true;

            return state.PlayerUserId == userId;
        }

        // =====================================================================
        //  サーバー側: マルチプレイ全滅判定
        // =====================================================================

        private readonly HashSet<string> _deadPlayerIds = new();
        private int _totalPlayerCount;

        /// <summary>
        /// 期待プレイヤー数を設定する。全滅判定の分母として使用。
        /// <para><b>呼び出し元:</b> SurvivorServerSession.NotifyPlayersReadyAsync</para>
        /// </summary>
        [Server]
        public void SetTotalPlayerCount(int count)
        {
            _totalPlayerCount = count;
            _deadPlayerIds.Clear();
        }

        /// <summary>
        /// プレイヤー死亡を記録する。全プレイヤーが死亡した場合、
        /// <see cref="NotifyGameEndedClientRpc"/> で GameOver を全クライアントに通知する。
        /// <para><b>呼び出し元:</b> SurvivorPlayerController.States.DeadState.Enter (サーバー側 #if UNITY_SERVER)</para>
        /// </summary>
        [Server]
        public void OnPlayerDied(string userId)
        {
            _deadPlayerIds.Add(userId);
            Debug.Log($"[NetworkSurvivorGameManager] Player died: {userId} ({_deadPlayerIds.Count}/{_totalPlayerCount})");

            if (_totalPlayerCount > 0 && _deadPlayerIds.Count >= _totalPlayerCount)
            {
                Debug.Log("[NetworkSurvivorGameManager] All players dead, sending GameOver");
                var result = new SurvivorNetworkGameResult
                {
                    IsVictory = false,
                    ClearTime = Time.time
                };
                NotifyGameEndedClientRpc(result);
            }
        }

        // =====================================================================
        //  シーン準備完了トラッキング
        // =====================================================================

        private readonly HashSet<int> _sceneReadyConnIds = new();

        // OnAllClientsSceneReady → MessagePipe IPublisher に移行済み

        /// <summary>
        /// クライアントがシーン準備完了を通知した際にサーバーが呼び出す。
        /// 全クライアントの準備完了で OnAllClientsSceneReady を発火する。
        /// </summary>
        [Server]
        public void OnClientSceneReady(NetworkConnectionToClient conn)
        {
            _sceneReadyConnIds.Add(conn.connectionId);
            Debug.Log($"[NetworkSurvivorGameManager] Client scene ready: conn={conn.connectionId} ({_sceneReadyConnIds.Count}/{_totalPlayerCount})");

            if (_totalPlayerCount > 0 && _sceneReadyConnIds.Count >= _totalPlayerCount)
            {
                Debug.Log("[NetworkSurvivorGameManager] All clients scene ready!");
                _allClientsSceneReadyPub?.Publish(new SurvivorSignals.Session.AllClientsSceneReady());
            }
        }

        /// <summary>セッション開始時にリセット</summary>
        [Server]
        public void ResetSceneReadyTracking()
        {
            _sceneReadyConnIds.Clear();
        }

        // =====================================================================
        //  ライフサイクル
        // =====================================================================

        public override void OnStartServer()
        {
            Instance = this;
            Debug.Log("[NetworkSurvivorGameManager] Spawned on server");
        }

        public override void OnStartClient()
        {
            DontDestroyOnLoad(gameObject);
            Instance = this;

            // VContainer 注入診断: IPublisher が null の場合、ClientRpc → MessagePipe パスが機能しない
            var nullPubs = new System.Text.StringBuilder();
            if (_allPlayersReadyPub == null) nullPubs.Append("AllPlayersReady,");
            if (_gameStartedPub == null) nullPubs.Append("GameStarted,");
            if (_gameEndedPub == null) nullPubs.Append("GameEnded,");
            if (_playerDamagedPub == null) nullPubs.Append("PlayerDamaged,");
            if (_playerDiedPub == null) nullPubs.Append("PlayerDied,");
            if (_waveStartedPub == null) nullPubs.Append("WaveStarted,");
            if (_waveClearedPub == null) nullPubs.Append("WaveCleared,");
            if (_enemyKilledPub == null) nullPubs.Append("EnemyKilled,");

            if (nullPubs.Length > 0)
                Debug.LogWarning($"[NetworkSurvivorGameManager] NULL IPublisher on client: {nullPubs}");
            else
                Debug.Log("[NetworkSurvivorGameManager] All IPublisher fields injected OK");

            Debug.Log("[NetworkSurvivorGameManager] Spawned on client");
        }

        public override void OnStopServer()
        {
            if (Instance == this) Instance = null;
        }

        public override void OnStopClient()
        {
            if (Instance == this) Instance = null;
        }
    }

    public enum WeaponApplyType { AddOrUpgrade, Replace }

    public struct WeaponApplyRequest
    {
        public int WeaponId;
        public bool IsNewWeapon;
        public WeaponApplyType Type;
        public int RemoveWeaponId;
    }
}
