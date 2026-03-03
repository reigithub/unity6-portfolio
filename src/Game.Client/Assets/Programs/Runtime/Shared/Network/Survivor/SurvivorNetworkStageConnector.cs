using System;
using Cysharp.Threading.Tasks;
using kcp2k;
using Mirror;
using UnityEngine;

namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// クライアント側 Mirror 接続管理。
    /// SurvivorStageScene.ReadyState から呼ばれ、サーバーへ接続する。
    /// </summary>
    public class SurvivorNetworkStageConnector : ISurvivorNetworkStageConnector
    {
        private NetworkManager _networkManager;
        private bool _isConnecting;

        public bool IsConnected => NetworkClient.isConnected;

        /// <summary>Mirror サーバーに接続する。</summary>
        public async UniTask ConnectAsync(string address, ushort port, int stageId, string sessionToken = "")
        {
            if (_isConnecting || IsConnected) return;
            _isConnecting = true;

            try
            {
                await EnsureNetworkManagerAsync();

                // KCP Transport 設定
                _networkManager.TryGetComponent<KcpTransport>(out var transport);
                if (transport == null)
                {
                    transport = _networkManager.gameObject.AddComponent<KcpTransport>();
                }
                Transport.active = transport;
                transport.port = port;
                _networkManager.networkAddress = address;

                // 認証ペイロード設定（Authenticator が OnClientAuthenticate で送信）
                SurvivorNetworkAuthenticator.PendingPayload =
                    SurvivorNetworkConnectionPayload.Encode(stageId, sessionToken);

                _networkManager.StartClient();

                // 接続完了待機（タイムアウト 10 秒）
                var timeout = Time.realtimeSinceStartup + 10f;
                while (!NetworkClient.isConnected)
                {
                    if (Time.realtimeSinceStartup > timeout)
                        throw new TimeoutException("Mirror connection timed out");
                    if (!NetworkClient.active)
                        throw new InvalidOperationException("Mirror client stopped");
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
            if (NetworkClient.isConnected || NetworkClient.active)
            {
                _networkManager?.StopClient();
                Debug.Log("[NetworkSurvivorStageClient] Disconnected");
            }
        }

        private async UniTask EnsureNetworkManagerAsync()
        {
            _networkManager = NetworkManager.singleton;
            if (_networkManager == null)
            {
                var go = new GameObject("[NetworkManager]");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _networkManager = go.AddComponent<NetworkManager>();
                var transport = go.AddComponent<KcpTransport>();
                _networkManager.transport = transport;
                Transport.active = transport;
                var auth = go.AddComponent<SurvivorNetworkAuthenticator>();
                _networkManager.authenticator = auth;
                await RegisterSpawnPrefabsAsync(_networkManager);
            }
        }

        private static async UniTask RegisterSpawnPrefabsAsync(NetworkManager nm)
        {
            var registry = await SurvivorNetworkPrefabs.LoadAsync();
            if (registry == null || registry.Prefabs == null) return;

            foreach (var prefab in registry.Prefabs)
            {
                if (prefab != null)
                    nm.spawnPrefabs.Add(prefab);
            }
        }

        public void Dispose() => Disconnect();
    }
}
