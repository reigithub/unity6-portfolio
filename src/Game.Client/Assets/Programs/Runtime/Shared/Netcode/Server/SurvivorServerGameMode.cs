#if UNITY_SERVER
using Cysharp.Threading.Tasks;
using Game.Shared.Netcode.Survivor;
using Unity.Netcode;
using UnityEngine;

namespace Game.Shared.Netcode.Server
{
    /// <summary>
    /// Survivor モード固有のサーバーロジック。
    /// ペイロードデコード・ステージ管理・セッション管理・シングルトンスポーンを担当する。
    /// </summary>
    public class SurvivorServerGameMode : MonoBehaviour, IServerGameMode
    {
        public static SurvivorServerGameMode Instance { get; private set; }

        private int _stageId;
        private bool _stageLoaded;
        private bool _sessionStarted;

        private GameObject _gameManagerInstance;
        private GameObject _enemyStateInstance;
        private GameObject _itemSyncInstance;

        private void Awake() => Instance = this;

        public void Initialize()
        {
            _stageLoaded = false;
            Debug.Log("[SurvivorServerGameMode] Initialized (waiting for client)");
        }

        public void OnConnectionApproval(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            // Survivor 固有ペイロードをデコード
            var (stageId, token) = NetworkSurvivorConnectionPayload.Decode(request.Payload);

            Debug.Log($"[SurvivorServerGameMode] Approval: client={request.ClientNetworkId} " +
                      $"stageId={stageId}, payload={request.Payload?.Length ?? 0} bytes");

            // stageId 設定（初回で確定）
            if (!_stageLoaded)
            {
                _stageId = stageId;
                _stageLoaded = true;
                Debug.Log($"[SurvivorServerGameMode] Stage set to {stageId}");
            }
            else if (_stageId != stageId)
            {
                Debug.LogWarning($"[SurvivorServerGameMode] StageId mismatch: " +
                                 $"expected={_stageId}, received={stageId}");
            }

            // Phase 2: 常に承認（Phase 3 で token 検証追加予定）
            response.Approved = true;
            response.CreatePlayerObject = false;
            response.Pending = false;

            Debug.Log($"[SurvivorServerGameMode] Client {request.ClientNetworkId} approved");
        }

        public void OnClientConnected(ulong clientId)
        {
            Debug.Log($"[SurvivorServerGameMode] Client connected: {clientId}");

            // セッション開始（初回のみ）
            if (!_sessionStarted && _stageLoaded)
            {
                _sessionStarted = true;
                SpawnSessionSingletons();
                Debug.Log("[SurvivorServerGameMode] Session started — singletons spawned");
            }

            SpawnPlayerState(clientId);
            NotifyPlayersReadyAsync().Forget();
        }

        public void OnClientDisconnected(ulong clientId)
        {
            Debug.Log($"[SurvivorServerGameMode] Client disconnected: {clientId}");
        }

        public void Cleanup()
        {
            if (_gameManagerInstance != null) Destroy(_gameManagerInstance);
            if (_enemyStateInstance != null) Destroy(_enemyStateInstance);
            if (_itemSyncInstance != null) Destroy(_itemSyncInstance);
            _gameManagerInstance = null;
            _enemyStateInstance = null;
            _itemSyncInstance = null;
        }

        private void SpawnSessionSingletons()
        {
            _gameManagerInstance = SpawnSingleton<NetworkSurvivorGameManager>();
            _enemyStateInstance = SpawnSingleton<NetworkSurvivorEnemyState>();
            _itemSyncInstance = SpawnSingleton<NetworkSurvivorItemSync>();
        }

        private void SpawnPlayerState(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            foreach (var prefab in nm.NetworkConfig.Prefabs.Prefabs)
            {
                if (prefab.Prefab.GetComponent<NetworkSurvivorPlayerState>() != null)
                {
                    var instance = Instantiate(prefab.Prefab);
                    instance.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
                    Debug.Log($"[SurvivorServerGameMode] PlayerState spawned for client {clientId}");
                    return;
                }
            }
            Debug.LogError("[SurvivorServerGameMode] NetworkSurvivorPlayerState prefab not found");
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
            Debug.LogError($"[SurvivorServerGameMode] Prefab with {typeof(T).Name} not found");
            return null;
        }

        private async UniTaskVoid NotifyPlayersReadyAsync()
        {
            // NetworkBehaviour の OnNetworkSpawn が完了するまで待機
            await UniTask.NextFrame();

            var gm = NetworkSurvivorGameManager.Instance;
            if (gm != null)
            {
                gm.NotifyAllPlayersReadyClientRpc();
                gm.NotifyGameStartedClientRpc(Time.time);
                Debug.Log("[SurvivorServerGameMode] AllPlayersReady + GameStarted sent");
            }
            else
            {
                Debug.LogWarning("[SurvivorServerGameMode] NetworkSurvivorGameManager not found");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
#endif