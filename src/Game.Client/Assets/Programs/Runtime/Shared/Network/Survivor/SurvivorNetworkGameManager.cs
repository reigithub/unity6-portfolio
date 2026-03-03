using System.Collections.Generic;
using Game.Shared.Survivor;
using MessagePipe;
using Mirror;
using Unity.Collections;
using UnityEngine;
using VContainer;

namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// ゲーム全体のイベント配信 NetworkBehaviour（シングルトン）。
    /// IGameStageHubReceiver の 19 コールバックに対応する ClientRpc を定義。
    /// ClientRpc は MessagePipe の IPublisher 経由でシグナルを配信する。
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

        // --- セッション ---

        [ClientRpc]
        public void NotifyAllPlayersReadyClientRpc()
        {
            Debug.Log("[NetworkSurvivorGameManager] AllPlayersReady");
            if (!isServer)
            {
                _allPlayersReadyPub?.Publish(new SurvivorSignals.Session.AllPlayersReady());
            }
        }

        [ClientRpc]
        public void NotifyGameStartedClientRpc(float serverTime)
        {
            Debug.Log($"[NetworkSurvivorGameManager] GameStarted at serverTime={serverTime}");
            if (!isServer)
            {
                _gameStartedPub?.Publish(new SurvivorSignals.Session.GameStarted(serverTime));
            }
        }

        // --- プレイヤーイベント ---

        [ClientRpc]
        public void NotifyPlayerDamagedClientRpc(FixedString64Bytes userId, int damage, int currentHp)
        {
            if (!isServer && IsLocalPlayer(userId))
            {
                _playerDamagedPub?.Publish(
                    new SurvivorSignals.Player.DamageReceived(damage, currentHp));
            }
        }

        [ClientRpc]
        public void NotifyPlayerDiedClientRpc(FixedString64Bytes userId)
        {
            if (!isServer && IsLocalPlayer(userId))
            {
                _playerDiedPub?.Publish(new SurvivorSignals.Player.Died());
            }
        }

        [ClientRpc]
        public void NotifyItemCollectedClientRpc(FixedString64Bytes userId, int itemId, int effectValue)
        {
            if (!isServer)
            {
                _itemCollectedPub?.Publish(
                    new SurvivorSignals.Player.ItemCollected(userId.ToString(), itemId, effectValue));
            }
        }

        [ClientRpc]
        public void NotifyPlayerLevelUpClientRpc(FixedString64Bytes userId, int newLevel, SurvivorNetworkWeaponUpgradeOption[] options)
        {
            if (!isServer)
            {
                _playerLeveledUpPub?.Publish(
                    new SurvivorSignals.Player.LeveledUp(userId.ToString(), newLevel, options));
            }
        }

        [ClientRpc]
        public void NotifyWeaponChangedClientRpc(FixedString64Bytes userId, int weaponId, int level, bool isNew)
        {
            if (!isServer)
            {
                _weaponChangedPub?.Publish(
                    new SurvivorSignals.Player.WeaponChanged(userId.ToString(), weaponId, level, isNew));
            }
        }

        // --- 敵・スコア ---

        [ClientRpc]
        public void NotifyEnemyKilledClientRpc(FixedString64Bytes killerUserId, int enemyId, int scoreGained, int totalKills)
        {
            if (!isServer)
            {
                _enemyKilledPub?.Publish(
                    new SurvivorSignals.Enemy.Killed(killerUserId.ToString(), enemyId, scoreGained, totalKills));
            }
        }

        // --- ウェーブ ---

        [ClientRpc]
        public void NotifyWaveClearedClientRpc(int waveNumber, int nextWaveNumber, int waveClearScore)
        {
            if (!isServer)
            {
                _waveClearedPub?.Publish(
                    new SurvivorSignals.Wave.Completed(waveNumber, waveClearScore));
            }
        }

        [ClientRpc]
        public void NotifyWaveStartedClientRpc(int waveNumber, int targetKills, int totalEnemies)
        {
            if (!isServer)
            {
                _waveStartedPub?.Publish(
                    new SurvivorSignals.Wave.Started(waveNumber, targetKills, totalEnemies));
            }
        }

        [ClientRpc]
        public void NotifyAllWavesClearedClientRpc()
        {
            if (!isServer)
            {
                _allWavesClearedPub?.Publish(new SurvivorSignals.Wave.AllCleared());
            }
        }

        [ClientRpc]
        public void NotifyTimeUpClientRpc()
        {
            if (!isServer)
            {
                _timeUpPub?.Publish(new SurvivorSignals.Wave.TimeUp());
            }
        }

        // --- ゲーム終了 ---

        [ClientRpc]
        public void NotifyGameEndedClientRpc(SurvivorNetworkGameResult result)
        {
            if (!isServer)
            {
                _gameEndedPub?.Publish(new SurvivorSignals.Game.Ended(result));
            }
        }

        // --- ポーズ ---

        [ClientRpc]
        public void NotifyGamePausedClientRpc(FixedString64Bytes requestedByUserId)
        {
            if (!isServer)
            {
                _gamePausedPub?.Publish(
                    new SurvivorSignals.Game.Paused(requestedByUserId.ToString()));
            }
        }

        [ClientRpc]
        public void NotifyGameResumedClientRpc()
        {
            if (!isServer)
            {
                _gameResumedPub?.Publish(new SurvivorSignals.Game.Resumed());
            }
        }

        // --- 接続 ---

        [ClientRpc]
        public void NotifyPlayerConnectedClientRpc(FixedString64Bytes userId, FixedString64Bytes playerName)
        {
            if (!isServer)
            {
                _playerConnectedPub?.Publish(
                    new SurvivorSignals.Connection.PlayerConnected(userId.ToString(), playerName.ToString()));
            }
        }

        [ClientRpc]
        public void NotifyPlayerDisconnectedClientRpc(FixedString64Bytes userId, FixedString64Bytes playerName)
        {
            if (!isServer)
            {
                _playerDisconnectedPub?.Publish(
                    new SurvivorSignals.Connection.PlayerDisconnected(userId.ToString(), playerName.ToString()));
            }
        }

        // --- ローカルプレイヤー判定（クライアント側フィルタリング用） ---

        /// <summary>
        /// ClientRpc 受信時に、対象 userId がローカルプレイヤーかどうかを判定する。
        /// SP モード (userId 空) はフォールバックで常に true。
        /// </summary>
        private bool IsLocalPlayer(FixedString64Bytes userId)
        {
            // SP モード: userId が空 → 常にローカル
            var userIdStr = userId.ToString();
            if (string.IsNullOrEmpty(userIdStr)) return true;

            var localPlayer = NetworkClient.localPlayer;
            if (localPlayer == null) return true;

            var state = localPlayer.GetComponent<SurvivorNetworkPlayerState>();
            if (state == null) return true;

            return state.PlayerUserId == userId;
        }

        // --- サーバー側: マルチプレイ全滅判定 ---

        private readonly HashSet<string> _deadPlayerIds = new();
        private int _totalPlayerCount;

        /// <summary>
        /// サーバー側: 期待プレイヤー数を設定する。SurvivorServerSession から呼ばれる。
        /// </summary>
        [Server]
        public void SetTotalPlayerCount(int count)
        {
            _totalPlayerCount = count;
            _deadPlayerIds.Clear();
        }

        /// <summary>
        /// サーバー側: プレイヤー死亡を記録し、全滅なら GameOver を通知。
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

        // --- ライフサイクル ---

        public override void OnStartServer()
        {
            Instance = this;
            Debug.Log("[NetworkSurvivorGameManager] Spawned on server");
        }

        public override void OnStartClient()
        {
            Instance = this;
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
}
