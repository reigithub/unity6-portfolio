using System;
using Cysharp.Threading.Tasks;
using Game.Shared.Netcode.Survivor;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Game.Shared.Netcode.Client
{
    /// <summary>
    /// クライアント側 NGO 接続管理。
    /// SurvivorStageScene.ReadyState から呼ばれ、サーバーへ接続する。
    /// </summary>
    public class NetworkSurvivorStageConnector : INetworkSurvivorStageConnector
    {
        private NetworkManager _networkManager;
        private bool _isConnecting;

        public bool IsConnected =>
            _networkManager != null && _networkManager.IsConnectedClient;

        /// <summary>NGO サーバーに接続する。</summary>
        public async UniTask ConnectAsync(string address, ushort port, int stageId, string sessionToken = "")
        {
            if (_isConnecting || IsConnected) return;
            _isConnecting = true;

            try
            {
                EnsureNetworkManager();

                _networkManager.TryGetComponent<UnityTransport>(out var transport);
                if (transport == null)
                {
                    transport = _networkManager.gameObject.AddComponent<UnityTransport>();
                    _networkManager.NetworkConfig.NetworkTransport = transport;
                }
                transport.SetConnectionData(address, port);

                _networkManager.NetworkConfig.ConnectionData =
                    NetworkSurvivorConnectionPayload.Encode(stageId, sessionToken);

                _networkManager.StartClient();

                // 接続完了待機（タイムアウト 10 秒）
                var timeout = Time.realtimeSinceStartup + 10f;
                while (!_networkManager.IsConnectedClient)
                {
                    if (Time.realtimeSinceStartup > timeout)
                        throw new TimeoutException("NGO connection timed out");
                    if (!_networkManager.IsClient)
                        throw new InvalidOperationException("NGO client stopped");
                    await UniTask.Yield();
                }

                Debug.Log($"[NetworkSurvivorStageClient] Connected to {address}:{port}");
            }
            finally
            {
                _isConnecting = false;
            }
        }

        public void Disconnect()
        {
            if (_networkManager != null && _networkManager.IsClient)
            {
                _networkManager.Shutdown();
                Debug.Log("[NetworkSurvivorStageClient] Disconnected");
            }
        }

        private void EnsureNetworkManager()
        {
            _networkManager = NetworkManager.Singleton;
            if (_networkManager == null)
            {
                var go = new GameObject("[NetworkManager]");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _networkManager = go.AddComponent<NetworkManager>();
                go.AddComponent<UnityTransport>();
                _networkManager.NetworkConfig.NetworkTransport =
                    go.GetComponent<UnityTransport>();
            }
        }

        public void Dispose() => Disconnect();
    }
}
