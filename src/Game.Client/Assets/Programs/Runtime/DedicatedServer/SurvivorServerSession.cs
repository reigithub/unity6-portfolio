using Cysharp.Threading.Tasks;
using Game.Shared.Network.Survivor;
using Unity.Netcode;
using UnityEngine;

namespace Game.Shared.Netcode.Server
{
    /// <summary>
    /// Survivor モード固有のサーバーセッション。
    /// インゲーム開始直前に StartSession() で NGO コールバックを登録し、
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
        /// セッション開始。NM のコールバックを登録する。
        /// インゲーム開始直前に呼ばれる。
        /// </summary>
        public void StartSession()
        {
            var nm = NetworkManager.Singleton;
            nm.ConnectionApprovalCallback = OnConnectionApproval;
            nm.OnClientConnectedCallback += OnClientConnected;
            nm.OnClientDisconnectCallback += OnClientDisconnected;
            Debug.Log("[SurvivorServerSession] Session started");
        }

        /// <summary>
        /// セッション終了。コールバック解除 + スポーン済みオブジェクトの破棄。
        /// </summary>
        public void StopSession()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                nm.ConnectionApprovalCallback = null;
                nm.OnClientConnectedCallback -= OnClientConnected;
                nm.OnClientDisconnectCallback -= OnClientDisconnected;
            }

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

        private void OnConnectionApproval(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            // Survivor 固有ペイロードをデコード
            var (stageId, token) = SurvivorNetworkConnectionPayload.Decode(request.Payload);

            Debug.Log($"[SurvivorServerSession] Approval: client={request.ClientNetworkId} " +
                      $"stageId={stageId}, payload={request.Payload?.Length ?? 0} bytes");

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

            // Phase 2: 常に承認（Phase 3 で token 検証追加予定）
            response.Approved = true;
            response.CreatePlayerObject = false;
            response.Pending = false;

            Debug.Log($"[SurvivorServerSession] Client {request.ClientNetworkId} approved");
        }

        private void OnClientConnected(ulong clientId)
        {
            Debug.Log($"[SurvivorServerSession] Client connected: {clientId}");

            // セッション開始（初回のみ）
            if (!_sessionStarted && _stageLoaded)
            {
                _sessionStarted = true;
                SpawnSessionSingletons();
                Debug.Log("[SurvivorServerSession] Singletons spawned");
            }

            SpawnPlayerState(clientId);
            NotifyPlayersReadyAsync().Forget();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            Debug.Log($"[SurvivorServerSession] Client disconnected: {clientId}");
        }

        private void SpawnSessionSingletons()
        {
            _gameManagerInstance = SpawnSingleton<SurvivorNetworkGameManager>();
            _enemyStateInstance = SpawnSingleton<SurvivorNetworkEnemyState>();
            _itemSyncInstance = SpawnSingleton<SurvivorNetworkItemSync>();
        }

        private void SpawnPlayerState(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            foreach (var prefab in nm.NetworkConfig.Prefabs.Prefabs)
            {
                if (prefab.Prefab.GetComponent<SurvivorNetworkPlayerState>() != null)
                {
                    var instance = Instantiate(prefab.Prefab);
                    instance.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
                    Debug.Log($"[SurvivorServerSession] PlayerState spawned for client {clientId}");
                    return;
                }
            }
            Debug.LogError("[SurvivorServerSession] NetworkSurvivorPlayerState prefab not found");
        }

        private GameObject SpawnSingleton<T>() where T : NetworkBehaviour
        {
            var nm = NetworkManager.Singleton;
            foreach (var prefab in nm.NetworkConfig.Prefabs.Prefabs)
            {
                if (prefab.Prefab.GetComponent<T>() != null)
                {
                    var instance = Instantiate(prefab.Prefab);
                    instance.GetComponent<NetworkObject>().Spawn();
                    return instance;
                }
            }
            Debug.LogError($"[SurvivorServerSession] Prefab with {typeof(T).Name} not found");
            return null;
        }

        private async UniTaskVoid NotifyPlayersReadyAsync()
        {
            // NetworkBehaviour の OnNetworkSpawn が完了するまで待機
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
