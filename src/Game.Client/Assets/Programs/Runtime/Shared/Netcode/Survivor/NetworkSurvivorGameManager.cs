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

        [Inject] private IPublisher<SurvivorNetworkSignals.AllPlayersReady> _allPlayersReadyPub;
        [Inject] private IPublisher<SurvivorNetworkSignals.GameStarted> _gameStartedPub;
        [Inject] private IPublisher<SurvivorNetworkSignals.GameEnded> _gameEndedPub;
        [Inject] private IPublisher<SurvivorNetworkSignals.PlayerConnected> _playerConnectedPub;
        [Inject] private IPublisher<SurvivorNetworkSignals.PlayerDisconnected> _playerDisconnectedPub;
        [Inject] private IPublisher<SurvivorNetworkSignals.PlayerDamaged> _playerDamagedPub;
        [Inject] private IPublisher<SurvivorNetworkSignals.PlayerDied> _playerDiedPub;
        [Inject] private IPublisher<SurvivorNetworkSignals.ItemCollected> _itemCollectedPub;
        [Inject] private IPublisher<SurvivorNetworkSignals.PlayerLeveledUp> _playerLeveledUpPub;
        [Inject] private IPublisher<SurvivorNetworkSignals.WeaponChanged> _weaponChangedPub;
        [Inject] private IPublisher<SurvivorNetworkSignals.EnemyKilled> _enemyKilledPub;
        [Inject] private IPublisher<SurvivorNetworkSignals.WaveStarted> _waveStartedPub;
        [Inject] private IPublisher<SurvivorNetworkSignals.WaveCleared> _waveClearedPub;
        [Inject] private IPublisher<SurvivorNetworkSignals.AllWavesCleared> _allWavesClearedPub;
        [Inject] private IPublisher<SurvivorNetworkSignals.TimeUp> _timeUpPub;
        [Inject] private IPublisher<SurvivorNetworkSignals.GamePaused> _gamePausedPub;
        [Inject] private IPublisher<SurvivorNetworkSignals.GameResumed> _gameResumedPub;

        // --- セッション ---

        [ClientRpc]
        public void NotifyAllPlayersReadyClientRpc()
        {
            Debug.Log("[NetworkSurvivorGameManager] AllPlayersReady");
            if (!IsServer)
            {
                _allPlayersReadyPub?.Publish(new SurvivorNetworkSignals.AllPlayersReady());
            }
        }

        [ClientRpc]
        public void NotifyGameStartedClientRpc(float serverTime)
        {
            Debug.Log($"[NetworkSurvivorGameManager] GameStarted at serverTime={serverTime}");
            if (!IsServer)
            {
                _gameStartedPub?.Publish(new SurvivorNetworkSignals.GameStarted(serverTime));
            }
        }

        // --- プレイヤーイベント ---

        [ClientRpc]
        public void NotifyPlayerDamagedClientRpc(FixedString64Bytes userId, int damage, int currentHp)
        {
            if (!IsServer)
            {
                _playerDamagedPub?.Publish(
                    new SurvivorNetworkSignals.PlayerDamaged(userId.ToString(), damage, currentHp));
            }
        }

        [ClientRpc]
        public void NotifyPlayerDiedClientRpc(FixedString64Bytes userId)
        {
            if (!IsServer)
            {
                _playerDiedPub?.Publish(new SurvivorNetworkSignals.PlayerDied(userId.ToString()));
            }
        }

        [ClientRpc]
        public void NotifyItemCollectedClientRpc(FixedString64Bytes userId, int itemId, int effectValue)
        {
            if (!IsServer)
            {
                _itemCollectedPub?.Publish(
                    new SurvivorNetworkSignals.ItemCollected(userId.ToString(), itemId, effectValue));
            }
        }

        [ClientRpc]
        public void NotifyPlayerLevelUpClientRpc(FixedString64Bytes userId, int newLevel, NetworkSurvivorWeaponUpgradeOption[] options)
        {
            if (!IsServer)
            {
                _playerLeveledUpPub?.Publish(
                    new SurvivorNetworkSignals.PlayerLeveledUp(userId.ToString(), newLevel, options));
            }
        }

        [ClientRpc]
        public void NotifyWeaponChangedClientRpc(FixedString64Bytes userId, int weaponId, int level, bool isNew)
        {
            if (!IsServer)
            {
                _weaponChangedPub?.Publish(
                    new SurvivorNetworkSignals.WeaponChanged(userId.ToString(), weaponId, level, isNew));
            }
        }

        // --- 敵・スコア ---

        [ClientRpc]
        public void NotifyEnemyKilledClientRpc(FixedString64Bytes killerUserId, int enemyId, int scoreGained, int totalKills)
        {
            if (!IsServer)
            {
                _enemyKilledPub?.Publish(
                    new SurvivorNetworkSignals.EnemyKilled(killerUserId.ToString(), enemyId, scoreGained, totalKills));
            }
        }

        // --- ウェーブ ---

        [ClientRpc]
        public void NotifyWaveClearedClientRpc(int waveNumber, int nextWaveNumber, int waveClearScore)
        {
            if (!IsServer)
            {
                _waveClearedPub?.Publish(
                    new SurvivorNetworkSignals.WaveCleared(waveNumber, nextWaveNumber, waveClearScore));
            }
        }

        [ClientRpc]
        public void NotifyWaveStartedClientRpc(int waveNumber, int targetKills, int totalEnemies)
        {
            if (!IsServer)
            {
                _waveStartedPub?.Publish(
                    new SurvivorNetworkSignals.WaveStarted(waveNumber, targetKills, totalEnemies));
            }
        }

        [ClientRpc]
        public void NotifyAllWavesClearedClientRpc()
        {
            if (!IsServer)
            {
                _allWavesClearedPub?.Publish(new SurvivorNetworkSignals.AllWavesCleared());
            }
        }

        [ClientRpc]
        public void NotifyTimeUpClientRpc()
        {
            if (!IsServer)
            {
                _timeUpPub?.Publish(new SurvivorNetworkSignals.TimeUp());
            }
        }

        // --- ゲーム終了 ---

        [ClientRpc]
        public void NotifyGameEndedClientRpc(NetworkSurvivorGameResult result)
        {
            if (!IsServer)
            {
                _gameEndedPub?.Publish(new SurvivorNetworkSignals.GameEnded(result));
            }
        }

        // --- ポーズ ---

        [ClientRpc]
        public void NotifyGamePausedClientRpc(FixedString64Bytes requestedByUserId)
        {
            if (!IsServer)
            {
                _gamePausedPub?.Publish(
                    new SurvivorNetworkSignals.GamePaused(requestedByUserId.ToString()));
            }
        }

        [ClientRpc]
        public void NotifyGameResumedClientRpc()
        {
            if (!IsServer)
            {
                _gameResumedPub?.Publish(new SurvivorNetworkSignals.GameResumed());
            }
        }

        // --- 接続 ---

        [ClientRpc]
        public void NotifyPlayerConnectedClientRpc(FixedString64Bytes userId, FixedString64Bytes playerName)
        {
            if (!IsServer)
            {
                _playerConnectedPub?.Publish(
                    new SurvivorNetworkSignals.PlayerConnected(userId.ToString(), playerName.ToString()));
            }
        }

        [ClientRpc]
        public void NotifyPlayerDisconnectedClientRpc(FixedString64Bytes userId, FixedString64Bytes playerName)
        {
            if (!IsServer)
            {
                _playerDisconnectedPub?.Publish(
                    new SurvivorNetworkSignals.PlayerDisconnected(userId.ToString(), playerName.ToString()));
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
