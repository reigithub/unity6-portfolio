using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Fusion;
using Game.Shared.Network.Fusion;
using Game.Shared.Signals.Survivor;
using MessagePipe;
using UnityEngine;

namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// Fusion セッション管理。プレイヤー接続追跡と AllPlayersReady 通知を担当。
    /// SurvivorNetworkStageConnector から生成され、SurvivorFusionRunner のコールバックを受ける。
    /// </summary>
    public class SurvivorFusionSession
    {
        private readonly IFusionRunnerService _runnerService;
        private readonly IPublisher<SurvivorSignals.Session.GameStarted> _gameStartedPub;
        private readonly IPublisher<SurvivorSignals.Session.AllPlayersDisconnected> _allPlayersDisconnectedPub;

        private readonly int _expectedPlayerCount;
        private readonly NetworkObject _playerPrefab;
        private readonly HashSet<PlayerRef> _connectedPlayers = new();
        private bool _allPlayersNotified;

        public int ConnectedPlayerCount => _connectedPlayers.Count;

        public SurvivorFusionSession(
            IFusionRunnerService runnerService,
            IPublisher<SurvivorSignals.Session.GameStarted> gameStartedPub,
            IPublisher<SurvivorSignals.Session.AllPlayersDisconnected> allPlayersDisconnectedPub,
            int expectedPlayerCount,
            NetworkObject playerPrefab)
        {
            _runnerService = runnerService;
            _gameStartedPub = gameStartedPub;
            _allPlayersDisconnectedPub = allPlayersDisconnectedPub;
            _expectedPlayerCount = expectedPlayerCount;
            _playerPrefab = playerPrefab;
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            _connectedPlayers.Add(player);
            Debug.Log($"[SurvivorFusionSession] Player joined: {player} ({ConnectedPlayerCount}/{_expectedPlayerCount})");

            // Spawn はステージシーンロード後に SpawnConnectedPlayers() で行う

            if (runner.IsServer && ConnectedPlayerCount >= _expectedPlayerCount && !_allPlayersNotified)
            {
                _allPlayersNotified = true;
                NotifyAllPlayersReadyAsync().Forget();
            }
        }

        /// <summary>
        /// 接続中の全プレイヤーを指定位置にスポーンする。
        /// ステージシーンロード後に SurvivorNetworkStageScene から呼ばれる。
        /// </summary>
        public void SpawnConnectedPlayers(NetworkRunner runner, Vector3 position, Quaternion rotation)
        {
            if (_playerPrefab == null)
            {
                Debug.LogError("[SurvivorFusionSession] Player prefab is null!");
                return;
            }

            foreach (var player in _connectedPlayers)
            {
                if (runner.GetPlayerObject(player) != null) continue;

                var playerObj = runner.Spawn(_playerPrefab, position, rotation, inputAuthority: player);
                runner.SetPlayerObject(player, playerObj);
                Debug.Log($"[SurvivorFusionSession] Spawned player {player} at {position}");
            }
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            // 切断時の Pause クリーンアップ (LevelUp 中切断で全体停止が永続化するのを防ぐ)
            if (_runnerService != null && _runnerService.TryGet<SurvivorFusionGameState>(out var gs))
            {
                gs.OnPlayerDisconnectedCleanup(player);
            }

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
                // リトライ時に再接続を受け入れるためリセット
                _allPlayersNotified = false;

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

            // ゲーム状態にプレイヤー数を設定（全滅判定用）
            if (_runnerService.TryGet<SurvivorFusionGameState>(out var gs))
            {
                gs.SetTotalPlayerCount(_expectedPlayerCount);

                // RPC で全クライアントに通知（MPPM 等では別 DI コンテナのため MessagePipe だけでは届かない）
                gs.RpcNotifyAllPlayersReady();
            }

            // サーバーローカルの GameStarted シグナル
            _gameStartedPub?.Publish(new SurvivorSignals.Session.GameStarted(Time.time));

            Debug.Log("[SurvivorFusionSession] AllPlayersReady (RPC) + GameStarted published");
        }
    }
}
