using Cysharp.Threading.Tasks;
using Game.Shared.Network.Survivor;
using Mirror;
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

        private GameObject _gameManagerInstance;
        private GameObject _enemyStateInstance;
        private GameObject _itemSyncInstance;

        private void Awake() => Instance = this;

        /// <summary>
        /// セッション開始。Mirror のコールバックを登録する。
        /// インゲーム開始直前に呼ばれる。
        /// </summary>
        public void StartSession()
        {
            SurvivorNetworkAuthenticator.OnPlayerAuthenticated += OnClientAuthenticated;
            NetworkServer.OnDisconnectedEvent += OnClientDisconnected;
            Debug.Log("[SurvivorServerSession] Session started");
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

            Debug.Log("[SurvivorServerSession] Session stopped");
        }

        private void OnClientAuthenticated(NetworkConnectionToClient conn, int stageId, string token)
        {
            Debug.Log($"[SurvivorServerSession] Client authenticated: conn={conn.connectionId}, stageId={stageId}");

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

            // セッション開始（初回のみ）
            if (!_sessionStarted && _stageLoaded)
            {
                _sessionStarted = true;
                SpawnSessionSingletons();
                Debug.Log("[SurvivorServerSession] Singletons spawned");
            }

            SpawnPlayerState(conn);
            NotifyPlayersReadyAsync().Forget();
        }

        private void OnClientDisconnected(NetworkConnectionToClient conn)
        {
            Debug.Log($"[SurvivorServerSession] Client disconnected: {conn.connectionId}");
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
                    NetworkServer.AddPlayerForConnection(conn, instance);
                    Debug.Log($"[SurvivorServerSession] PlayerState spawned for conn {conn.connectionId}");
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
