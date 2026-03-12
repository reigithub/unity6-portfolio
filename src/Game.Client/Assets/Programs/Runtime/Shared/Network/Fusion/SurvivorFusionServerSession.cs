using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Fusion;
using Game.Shared.Signals.Survivor;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Shared.Network.Fusion
{
    /// <summary>
    /// Fusion セッション管理。プレイヤー接続追跡と AllPlayersReady 通知を担当。
    /// SurvivorFusionStageConnector から生成され、SurvivorFusionRunner のコールバックを受ける。
    /// </summary>
    public class SurvivorFusionServerSession
    {
        private readonly IObjectResolver _resolver;
        private readonly IPublisher<SurvivorSignals.Session.AllPlayersReady> _allPlayersReadyPub;
        private readonly IPublisher<SurvivorSignals.Session.GameStarted> _gameStartedPub;
        private readonly IPublisher<SurvivorSignals.Session.AllPlayersDisconnected> _allPlayersDisconnectedPub;

        private readonly int _expectedPlayerCount;
        private readonly NetworkObject _playerPrefab;
        private readonly HashSet<PlayerRef> _connectedPlayers = new();
        private bool _allPlayersNotified;

        public int ConnectedPlayerCount => _connectedPlayers.Count;

        public SurvivorFusionServerSession(
            IObjectResolver resolver,
            IPublisher<SurvivorSignals.Session.AllPlayersReady> allPlayersReadyPub,
            IPublisher<SurvivorSignals.Session.GameStarted> gameStartedPub,
            IPublisher<SurvivorSignals.Session.AllPlayersDisconnected> allPlayersDisconnectedPub,
            int expectedPlayerCount,
            NetworkObject playerPrefab)
        {
            _resolver = resolver;
            _allPlayersReadyPub = allPlayersReadyPub;
            _gameStartedPub = gameStartedPub;
            _allPlayersDisconnectedPub = allPlayersDisconnectedPub;
            _expectedPlayerCount = expectedPlayerCount;
            _playerPrefab = playerPrefab;
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            _connectedPlayers.Add(player);
            Debug.Log($"[SurvivorFusionSession] Player joined: {player} ({ConnectedPlayerCount}/{_expectedPlayerCount})");

            // Server/Host: プレイヤー NetworkObject をスポーン
            if (runner.IsServer && _playerPrefab != null)
            {
                var playerObj = runner.Spawn(_playerPrefab, inputAuthority: player,
                    onBeforeSpawned: (_, obj) =>
                    {
                        _resolver.InjectGameObject(obj.gameObject);
                    });
                runner.SetPlayerObject(player, playerObj);
                Debug.Log($"[SurvivorFusionSession] Spawned player object for {player}");
            }

            if (runner.IsServer && ConnectedPlayerCount >= _expectedPlayerCount && !_allPlayersNotified)
            {
                _allPlayersNotified = true;
                NotifyAllPlayersReadyAsync().Forget();
            }
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            // プレイヤー NetworkObject をデスポーン
            var playerObj = runner.GetPlayerObject(player);
            if (playerObj != null)
            {
                runner.Despawn(playerObj);
            }

            _connectedPlayers.Remove(player);
            Debug.Log($"[SurvivorFusionSession] Player left: {player} ({ConnectedPlayerCount} remaining)");

            if (ConnectedPlayerCount <= 0 && _allPlayersNotified)
            {
                Debug.Log("[SurvivorFusionSession] All players disconnected");
                _allPlayersDisconnectedPub?.Publish(new SurvivorSignals.Session.AllPlayersDisconnected());
            }
        }

        /// <summary>
        /// AllPlayersReady を1フレーム遅延で発火する。
        /// SurvivorStageConnectScene の WaitForAllPlayersReadyAsync() が購読登録を完了した後に届くようにする。
        /// </summary>
        private async UniTaskVoid NotifyAllPlayersReadyAsync()
        {
            await UniTask.NextFrame();

            // ゲーム状態シングルトンにプレイヤー数を設定（全滅判定用）
            SurvivorFusionGameState.Instance?.SetTotalPlayerCount(_expectedPlayerCount);

            _allPlayersReadyPub?.Publish(new SurvivorSignals.Session.AllPlayersReady());
            _gameStartedPub?.Publish(new SurvivorSignals.Session.GameStarted(Time.time));

            Debug.Log("[SurvivorFusionSession] AllPlayersReady + GameStarted published");
        }
    }
}
