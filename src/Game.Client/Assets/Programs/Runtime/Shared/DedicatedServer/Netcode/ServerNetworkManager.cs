#if UNITY_SERVER
using System;
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

        private void OnClientConnected(ulong clientId)
        {
            Debug.Log($"[ServerNetworkManager] Client connected: {clientId}");
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

            if (Instance == this)
            {
                Instance = null;
            }
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
