#if UNITY_SERVER
using System;
using Game.Shared.Netcode.Survivor;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Game.Shared.DedicatedServer.Netcode
{
    /// <summary>
    /// Dedicated Server の NGO ライフサイクル管理。
    /// NetworkManager + UnityTransport を生成し、サーバーモードで起動する。
    /// </summary>
    public class ServerNetworkManager : MonoBehaviour
    {
        public static ServerNetworkManager Instance { get; private set; }

        private ushort _port = 7777;

        // セッション管理
        private GameObject _gameManagerInstance;
        private GameObject _enemyStateInstance;
        private GameObject _itemSyncInstance;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// NetworkManager を構成してサーバーを起動する。
        /// DedicatedServerBootstrap.Initialize() から呼ばれる。
        /// </summary>
        public void Initialize(ushort port)
        {
            _port = port;

            // --- NetworkManager + UnityTransport セットアップ ---
            var transport = gameObject.AddComponent<UnityTransport>();
            transport.SetConnectionData("0.0.0.0", _port);

            var nm = gameObject.AddComponent<NetworkManager>();
            nm.NetworkConfig.NetworkTransport = transport;

            // --- Connection Approval 有効化 ---
            nm.NetworkConfig.ConnectionApproval = true;
            nm.ConnectionApprovalCallback += ServerConnectionApproval.ApproveConnection;

            // --- イベントハンドラ ---
            nm.OnClientConnectedCallback += OnClientConnected;
            nm.OnClientDisconnectCallback += OnClientDisconnected;

            // --- サーバー起動 ---
            nm.StartServer();

            Debug.Log($"[ServerNetworkManager] NGO Server started on port {_port}");
        }

        /// <summary>
        /// NGO セッション開始 — シングルトン NetworkBehaviour 群をスポーン。
        /// Phase 3 ではサーバー起動直後に暫定呼び出し。Phase 4 以降でマッチ開始トリガーから呼ばれるように変更。
        /// </summary>
        public void StartSession()
        {
            _gameManagerInstance = SpawnSingleton<NetworkSurvivorGameManager>();
            _enemyStateInstance = SpawnSingleton<NetworkSurvivorEnemyState>();
            _itemSyncInstance = SpawnSingleton<NetworkSurvivorItemSync>();
            Debug.Log("[ServerNetworkManager] Session started — singletons spawned");
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
            Debug.LogError($"[ServerNetworkManager] Prefab with {typeof(T).Name} not found");
            return null;
        }

        private void OnClientConnected(ulong clientId)
        {
            Debug.Log($"[ServerNetworkManager] Client connected: {clientId}");
            SpawnPlayerState(clientId);
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
                    Debug.Log($"[ServerNetworkManager] NetworkSurvivorPlayerState spawned for client {clientId}");
                    return;
                }
            }
            Debug.LogError("[ServerNetworkManager] NetworkSurvivorPlayerState prefab not found");
        }

        private void OnClientDisconnected(ulong clientId)
        {
            Debug.Log($"[ServerNetworkManager] Client disconnected: {clientId}");
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.Shutdown();
            }

            CleanupSessionInstances();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void CleanupSessionInstances()
        {
            if (_gameManagerInstance != null) Destroy(_gameManagerInstance);
            if (_enemyStateInstance != null) Destroy(_enemyStateInstance);
            if (_itemSyncInstance != null) Destroy(_itemSyncInstance);
            _gameManagerInstance = null;
            _enemyStateInstance = null;
            _itemSyncInstance = null;
        }

        /// <summary>
        /// コマンドライン引数から --port を解析。デフォルト 7777。
        /// </summary>
        public static ushort ParsePort()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--port" && ushort.TryParse(args[i + 1], out ushort port))
                {
                    return port;
                }
            }
            return 7777;
        }
    }
}
#endif
