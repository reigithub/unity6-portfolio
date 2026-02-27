using Game.Shared.Survivor;
using MessagePipe;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace Game.Shared.Netcode.Survivor
{
    /// <summary>
    /// ゲーム全体のイベント配信 NetworkBehaviour（シングルトン）。
    /// IGameStageHubReceiver の 19 コールバックに対応する ClientRpc を定義。
    /// ClientRpc は MessagePipe の IPublisher 経由でシグナルを配信する。
    /// </summary>
    public class NetworkSurvivorGameManager : NetworkBehaviour
    {
        public static NetworkSurvivorGameManager Instance { get; private set; }

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
            if (!IsServer)
            {
                _allPlayersReadyPub?.Publish(new SurvivorSignals.Session.AllPlayersReady());
            }
        }

        [ClientRpc]
        public void NotifyGameStartedClientRpc(float serverTime)
        {
            Debug.Log($"[NetworkSurvivorGameManager] GameStarted at serverTime={serverTime}");
            if (!IsServer)
            {
                _gameStartedPub?.Publish(new SurvivorSignals.Session.GameStarted(serverTime));
            }
        }

        // --- プレイヤーイベント ---

        [ClientRpc]
        public void NotifyPlayerDamagedClientRpc(FixedString64Bytes userId, int damage, int currentHp)
        {
            if (!IsServer)
            {
                _playerDamagedPub?.Publish(
                    new SurvivorSignals.Player.DamageReceived(damage, currentHp));
            }
        }

        [ClientRpc]
        public void NotifyPlayerDiedClientRpc(FixedString64Bytes userId)
        {
            if (!IsServer)
            {
                _playerDiedPub?.Publish(new SurvivorSignals.Player.Died());
            }
        }

        [ClientRpc]
        public void NotifyItemCollectedClientRpc(FixedString64Bytes userId, int itemId, int effectValue)
        {
            if (!IsServer)
            {
                _itemCollectedPub?.Publish(
                    new SurvivorSignals.Player.ItemCollected(userId.ToString(), itemId, effectValue));
            }
        }

        [ClientRpc]
        public void NotifyPlayerLevelUpClientRpc(FixedString64Bytes userId, int newLevel, NetworkSurvivorWeaponUpgradeOption[] options)
        {
            if (!IsServer)
            {
                _playerLeveledUpPub?.Publish(
                    new SurvivorSignals.Player.LeveledUp(userId.ToString(), newLevel, options));
            }
        }

        [ClientRpc]
        public void NotifyWeaponChangedClientRpc(FixedString64Bytes userId, int weaponId, int level, bool isNew)
        {
            if (!IsServer)
            {
                _weaponChangedPub?.Publish(
                    new SurvivorSignals.Player.WeaponChanged(userId.ToString(), weaponId, level, isNew));
            }
        }

        // --- 敵・スコア ---

        [ClientRpc]
        public void NotifyEnemyKilledClientRpc(FixedString64Bytes killerUserId, int enemyId, int scoreGained, int totalKills)
        {
            if (!IsServer)
            {
                _enemyKilledPub?.Publish(
                    new SurvivorSignals.Enemy.Killed(killerUserId.ToString(), enemyId, scoreGained, totalKills));
            }
        }

        // --- ウェーブ ---

        [ClientRpc]
        public void NotifyWaveClearedClientRpc(int waveNumber, int nextWaveNumber, int waveClearScore)
        {
            if (!IsServer)
            {
                _waveClearedPub?.Publish(
                    new SurvivorSignals.Wave.Completed(waveNumber, waveClearScore));
            }
        }

        [ClientRpc]
        public void NotifyWaveStartedClientRpc(int waveNumber, int targetKills, int totalEnemies)
        {
            if (!IsServer)
            {
                _waveStartedPub?.Publish(
                    new SurvivorSignals.Wave.Started(waveNumber, targetKills, totalEnemies));
            }
        }

        [ClientRpc]
        public void NotifyAllWavesClearedClientRpc()
        {
            if (!IsServer)
            {
                _allWavesClearedPub?.Publish(new SurvivorSignals.Wave.AllCleared());
            }
        }

        [ClientRpc]
        public void NotifyTimeUpClientRpc()
        {
            if (!IsServer)
            {
                _timeUpPub?.Publish(new SurvivorSignals.Wave.TimeUp());
            }
        }

        // --- ゲーム終了 ---

        [ClientRpc]
        public void NotifyGameEndedClientRpc(NetworkSurvivorGameResult result)
        {
            if (!IsServer)
            {
                _gameEndedPub?.Publish(new SurvivorSignals.Game.Ended(result));
            }
        }

        // --- ポーズ ---

        [ClientRpc]
        public void NotifyGamePausedClientRpc(FixedString64Bytes requestedByUserId)
        {
            if (!IsServer)
            {
                _gamePausedPub?.Publish(
                    new SurvivorSignals.Game.Paused(requestedByUserId.ToString()));
            }
        }

        [ClientRpc]
        public void NotifyGameResumedClientRpc()
        {
            if (!IsServer)
            {
                _gameResumedPub?.Publish(new SurvivorSignals.Game.Resumed());
            }
        }

        // --- 接続 ---

        [ClientRpc]
        public void NotifyPlayerConnectedClientRpc(FixedString64Bytes userId, FixedString64Bytes playerName)
        {
            if (!IsServer)
            {
                _playerConnectedPub?.Publish(
                    new SurvivorSignals.Connection.PlayerConnected(userId.ToString(), playerName.ToString()));
            }
        }

        [ClientRpc]
        public void NotifyPlayerDisconnectedClientRpc(FixedString64Bytes userId, FixedString64Bytes playerName)
        {
            if (!IsServer)
            {
                _playerDisconnectedPub?.Publish(
                    new SurvivorSignals.Connection.PlayerDisconnected(userId.ToString(), playerName.ToString()));
            }
        }

        // --- ライフサイクル ---

        public override void OnNetworkSpawn()
        {
            Instance = this;
            Debug.Log($"[NetworkSurvivorGameManager] Spawned (IsServer={IsServer})");
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }
    }
}
