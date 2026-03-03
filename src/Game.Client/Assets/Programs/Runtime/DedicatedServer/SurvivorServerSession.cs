using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Shared.Network.Survivor;
using Mirror;
using Unity.Collections;
using UnityEngine;

namespace Game.Shared.Netcode.Server
{
    /// <summary>
    /// Survivor モード固有のサーバーセッション。
    /// インゲーム開始直前に StartSession() で Mirror コールバックを登録し、
    /// セッション終了時に StopSession() で解除 + クリーンアップする。
    /// </summary>
    public class SurvivorServerSession : MonoBehaviour
    {
        public static SurvivorServerSession Instance { get; private set; }

        private int _stageId;
        private bool _stageLoaded;
        private bool _sessionStarted;

        private int _expectedPlayerCount = 1;
        private int _connectedPlayerCount;
        private readonly HashSet<NetworkConnectionToClient> _authenticatedConnections = new();
        private readonly Dictionary<NetworkConnectionToClient, string> _connectionUserIds = new();

        private GameObject _gameManagerInstance;
        private GameObject _enemyStateInstance;
        private GameObject _itemSyncInstance;

        private void Awake() => Instance = this;

        /// <summary>
        /// セッション開始。Mirror のコールバックを登録する。
        /// </summary>
        public void StartSession(int expectedPlayerCount = 1)
        {
            _expectedPlayerCount = expectedPlayerCount;
            _connectedPlayerCount = 0;
            _authenticatedConnections.Clear();
            _connectionUserIds.Clear();

            SurvivorNetworkAuthenticator.OnPlayerAuthenticated += OnClientAuthenticated;
            NetworkServer.OnDisconnectedEvent += OnClientDisconnected;
            Debug.Log($"[SurvivorServerSession] Session started, expecting {_expectedPlayerCount} player(s)");
        }

        /// <summary>
        /// セッション終了。コールバック解除 + スポーン済みオブジェクトの破棄。
        /// </summary>
        public void StopSession()
        {
            SurvivorNetworkAuthenticator.OnPlayerAuthenticated -= OnClientAuthenticated;
            NetworkServer.OnDisconnectedEvent -= OnClientDisconnected;

            if (_gameManagerInstance != null) Destroy(_gameManagerInstance);
            if (_enemyStateInstance != null) Destroy(_enemyStateInstance);
            if (_itemSyncInstance != null) Destroy(_itemSyncInstance);
            _gameManagerInstance = null;
            _enemyStateInstance = null;
            _itemSyncInstance = null;

            _stageLoaded = false;
            _sessionStarted = false;
            _connectedPlayerCount = 0;
            _authenticatedConnections.Clear();
            _connectionUserIds.Clear();

            Debug.Log("[SurvivorServerSession] Session stopped");
        }

        private void OnClientAuthenticated(NetworkConnectionToClient conn, int stageId, string userId)
        {
            Debug.Log($"[SurvivorServerSession] Client authenticated: conn={conn.connectionId}, stageId={stageId}, userId={userId}");

            _authenticatedConnections.Add(conn);
            _connectionUserIds[conn] = userId;
            _connectedPlayerCount++;

            // stageId 設定（初回で確定）
            if (!_stageLoaded)
            {
                _stageId = stageId;
                _stageLoaded = true;
                Debug.Log($"[SurvivorServerSession] Stage set to {stageId}");
            }
            else if (_stageId != stageId)
            {
                Debug.LogWarning($"[SurvivorServerSession] StageId mismatch: " +
                                 $"expected={_stageId}, received={stageId}");
            }

            // セッション開始（初回のみ）: シングルトンスポーン
            if (!_sessionStarted && _stageLoaded)
            {
                _sessionStarted = true;
                SpawnSessionSingletons();
                Debug.Log("[SurvivorServerSession] Singletons spawned");
            }

            // プレイヤーステートスポーン（毎接続）
            SpawnPlayerState(conn);

            // 全員揃ったらゲーム開始
            if (_connectedPlayerCount >= _expectedPlayerCount)
            {
                NotifyPlayersReadyAsync().Forget();
            }
            else
            {
                Debug.Log($"[SurvivorServerSession] Waiting for players: {_connectedPlayerCount}/{_expectedPlayerCount}");
            }
        }

        private void OnClientDisconnected(NetworkConnectionToClient conn)
        {
            if (!_authenticatedConnections.Remove(conn))
            {
                Debug.Log($"[SurvivorServerSession] Unauthenticated client disconnected: {conn.connectionId}");
                return;
            }

            _connectionUserIds.TryGetValue(conn, out var userId);
            _connectionUserIds.Remove(conn);
            _connectedPlayerCount--;

            Debug.Log($"[SurvivorServerSession] Client disconnected: conn={conn.connectionId}, userId={userId}, remaining={_connectedPlayerCount}");

            // 残りプレイヤーに切断を通知
            var gm = SurvivorNetworkGameManager.Instance;
            if (gm != null && !string.IsNullOrEmpty(userId))
            {
                gm.NotifyPlayerDisconnectedClientRpc(new FixedString64Bytes(userId), new FixedString64Bytes(""));
            }

            // 全員切断 → セッション終了
            if (_connectedPlayerCount <= 0 && _sessionStarted)
            {
                Debug.Log("[SurvivorServerSession] All players disconnected, stopping session");
                StopSession();
            }
        }

        private void SpawnSessionSingletons()
        {
            _gameManagerInstance = SpawnSingleton<SurvivorNetworkGameManager>();
            _enemyStateInstance = SpawnSingleton<SurvivorNetworkEnemyState>();
            _itemSyncInstance = SpawnSingleton<SurvivorNetworkItemSync>();
        }

        private void SpawnPlayerState(NetworkConnectionToClient conn)
        {
            var nm = NetworkManager.singleton;
            foreach (var prefab in nm.spawnPrefabs)
            {
                if (prefab.GetComponent<SurvivorNetworkPlayerState>() != null)
                {
                    var instance = Instantiate(prefab);

                    // PlayerUserId を設定（SyncVar でクライアントに同期）
                    var playerState = instance.GetComponent<SurvivorNetworkPlayerState>();
                    if (_connectionUserIds.TryGetValue(conn, out var userId))
                    {
                        playerState.PlayerUserId = new FixedString64Bytes(userId);
                    }

                    NetworkServer.AddPlayerForConnection(conn, instance);
                    Debug.Log($"[SurvivorServerSession] PlayerState spawned for conn {conn.connectionId}, userId={userId}");
                    return;
                }
            }
            Debug.LogError("[SurvivorServerSession] NetworkSurvivorPlayerState prefab not found");
        }

        private GameObject SpawnSingleton<T>() where T : NetworkBehaviour
        {
            var nm = NetworkManager.singleton;
            foreach (var prefab in nm.spawnPrefabs)
            {
                if (prefab.GetComponent<T>() != null)
                {
                    var instance = Instantiate(prefab);
                    NetworkServer.Spawn(instance);
                    return instance;
                }
            }
            Debug.LogError($"[SurvivorServerSession] Prefab with {typeof(T).Name} not found");
            return null;
        }

        private async UniTaskVoid NotifyPlayersReadyAsync()
        {
            // NetworkBehaviour の OnStartServer/OnStartClient が完了するまで待機
            await UniTask.NextFrame();

            var gm = SurvivorNetworkGameManager.Instance;
            if (gm != null)
            {
                gm.SetTotalPlayerCount(_expectedPlayerCount);
                gm.NotifyAllPlayersReadyClientRpc();
                gm.NotifyGameStartedClientRpc(Time.time);
                Debug.Log("[SurvivorServerSession] AllPlayersReady + GameStarted sent");
            }
            else
            {
                Debug.LogWarning("[SurvivorServerSession] NetworkSurvivorGameManager not found");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
