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
    /// Editor SP では StartHostAsync で Host mode（Server + Client 同一プロセス）を起動。
    /// </summary>
    public class SurvivorNetworkStageConnector : ISurvivorNetworkStageConnector
    {
        private NetworkManager _networkManager;
        private bool _isConnecting;

        public bool IsConnected => NetworkClient.isConnected;

        /// <summary>Mirror サーバーに接続する（Client-only）。</summary>
        public async UniTask ConnectAsync(string address, ushort port, int stageId, string sessionToken = "")
        {
            if (_isConnecting || IsConnected) return;
            _isConnecting = true;

            try
            {
                await EnsureNetworkManagerAsync();
                SetupTransport(address, port);

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

                Debug.Log($"[SurvivorNetworkStageConnector] Connected to {address}:{port}");
            }
            finally
            {
                _isConnecting = false;
            }
        }

        /// <summary>
        /// Editor Host mode: サーバー + クライアントを同一プロセスで起動。
        /// SurvivorUnityServerSession が認証コールバック経由でシングルトンとプレイヤーをスポーンする。
        /// </summary>
        public async UniTask StartHostAsync(int stageId)
        {
            if (_isConnecting || IsConnected) return;
            _isConnecting = true;

            try
            {
                await EnsureNetworkManagerAsync();
                SetupTransport("localhost", 7777);

                // SP 認証ペイロード（トークン空 = SP 承認）
                SurvivorNetworkAuthenticator.PendingPayload =
                    SurvivorNetworkConnectionPayload.Encode(stageId, "");

                // サーバーセッション生成（Mirror コールバック登録）
                var sessionGo = new GameObject("[ServerSession]");
                UnityEngine.Object.DontDestroyOnLoad(sessionGo);
                var session = sessionGo.AddComponent<SurvivorUnityServerSession>();
                session.StartSession(SurvivorNetworkMatchConnector.ExpectedPlayerCount);

                // Host 起動 → Server 起動 + 自身を Client として接続
                // → Authenticator 発火 → OnClientAuthenticated → シングルトン + PlayerState スポーン
                _networkManager.StartHost();

                // 接続完了待機（タイムアウト 5 秒）
                var timeout = Time.realtimeSinceStartup + 5f;
                while (!NetworkClient.isConnected)
                {
                    if (Time.realtimeSinceStartup > timeout)
                        throw new TimeoutException("Host startup timed out");
                    await UniTask.Yield();
                }

                Debug.Log("[SurvivorNetworkStageConnector] Host mode started");
            }
            finally
            {
                _isConnecting = false;
            }
        }

        /// <summary>
        /// Editor Server-only mode: サーバーのみ起動（ローカルClient接続なし）。
        /// MPPM の Server タグ付きインスタンスで使用。外部 Client が接続してくるのを待つ。
        /// </summary>
        public async UniTask StartServerAsync(int stageId)
        {
            if (_isConnecting || NetworkServer.active) return;
            _isConnecting = true;

            try
            {
                await EnsureNetworkManagerAsync();
                SetupTransport("localhost", 7777);

                // サーバーセッション生成（Mirror コールバック登録）
                var sessionGo = new GameObject("[ServerSession]");
                UnityEngine.Object.DontDestroyOnLoad(sessionGo);
                var session = sessionGo.AddComponent<SurvivorUnityServerSession>();
                session.StartSession(SurvivorNetworkMatchConnector.ExpectedPlayerCount);

                // Server のみ起動（ローカル Client 接続なし）
                _networkManager.StartServer();

                // Server 起動確認
                var timeout = Time.realtimeSinceStartup + 5f;
                while (!NetworkServer.active)
                {
                    if (Time.realtimeSinceStartup > timeout)
                        throw new TimeoutException("Server startup timed out");
                    await UniTask.Yield();
                }

                Debug.Log("[SurvivorNetworkStageConnector] Server-only mode started, waiting for clients...");
            }
            finally
            {
                _isConnecting = false;
            }
        }

        public void Disconnect()
        {
            if (NetworkServer.active && NetworkClient.isConnected)
            {
                // Host mode: Server + ローカル Client 両方停止
                _networkManager?.StopHost();
                Debug.Log("[SurvivorNetworkStageConnector] Host stopped");
            }
            else if (NetworkServer.active)
            {
                // Server-only mode
                _networkManager?.StopServer();
                Debug.Log("[SurvivorNetworkStageConnector] Server stopped");
            }
            else if (NetworkClient.isConnected || NetworkClient.active)
            {
                _networkManager?.StopClient();
                Debug.Log("[SurvivorNetworkStageConnector] Disconnected");
            }

            // ServerSession の明示的クリーンアップ（DontDestroyOnLoad のため自動破棄されない）
            var session = SurvivorUnityServerSession.Instance;
            if (session != null)
            {
                session.StopSession();
                UnityEngine.Object.Destroy(session.gameObject);
            }
        }

        /// <summary>KCP Transport の共通設定。</summary>
        private void SetupTransport(string address, ushort port)
        {
            _networkManager.TryGetComponent<KcpTransport>(out var transport);
            if (transport == null)
            {
                transport = _networkManager.gameObject.AddComponent<KcpTransport>();
            }
            Transport.active = transport;
            transport.port = port;
            _networkManager.networkAddress = address;
        }

        private async UniTask EnsureNetworkManagerAsync()
        {
            _networkManager = NetworkManager.singleton;
            if (_networkManager == null)
            {
                var go = new GameObject("[NetworkManager]");
                UnityEngine.Object.DontDestroyOnLoad(go);
                var transport = go.AddComponent<KcpTransport>();
                Transport.active = transport;
                _networkManager = go.AddComponent<NetworkManager>();
                _networkManager.transport = transport;
                _networkManager.autoCreatePlayer = false;
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
