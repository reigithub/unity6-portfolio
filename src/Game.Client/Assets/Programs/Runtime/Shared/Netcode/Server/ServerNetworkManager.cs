#if UNITY_SERVER
using System;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Game.Shared.Netcode.Server
{
    /// <summary>
    /// Dedicated Server の NGO ライフサイクル管理。
    /// NetworkManager + UnityTransport を生成し、サーバーモードで起動する。
    /// ゲームモード固有ロジックは IServerGameMode に委譲する。
    /// </summary>
    public class ServerNetworkManager : MonoBehaviour
    {
        public static ServerNetworkManager Instance { get; private set; }

        private ushort _port = 7777;
        private IServerGameMode _gameMode;

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
        /// コンポーネント追加後、次フレームまで待機して内部初期化を完了させてから StartServer する。
        /// </summary>
        public void Initialize(ushort port)
        {
            _port = port;
            InitializeAsync().Forget();
        }

        /// <summary>
        /// ゲームモードを設定・切り替えする。
        /// サーバー起動前でも起動後でも呼び出し可能。
        /// 既存のゲームモードがある場合は Cleanup → コンポーネント破棄してから切り替える。
        /// </summary>
        public void SetGameMode(IServerGameMode gameMode)
        {
            if (_gameMode != null)
            {
                _gameMode.Cleanup();
                if (_gameMode is Component oldComponent)
                {
                    Destroy(oldComponent);
                }
            }
            _gameMode = gameMode;
            _gameMode.Initialize();
            Debug.Log($"[ServerNetworkManager] GameMode set: {gameMode.GetType().Name}");
        }

        private async UniTaskVoid InitializeAsync()
        {
            // --- NetworkManager + UnityTransport セットアップ ---
            var transport = gameObject.AddComponent<UnityTransport>();
            transport.SetConnectionData("0.0.0.0", _port);

            var nm = gameObject.AddComponent<NetworkManager>();
            nm.NetworkConfig = new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = transport;

            // --- Connection Approval 有効化 ---
            nm.NetworkConfig.ConnectionApproval = true;
            nm.ConnectionApprovalCallback += (req, res) => _gameMode.OnConnectionApproval(req, res);

            // --- イベントハンドラ ---
            nm.OnClientConnectedCallback += OnClientConnected;
            nm.OnClientDisconnectCallback += OnClientDisconnected;

            // NetworkManager の内部初期化を待機
            await UniTask.NextFrame();

            // --- サーバー起動 ---
            nm.StartServer();

            Debug.Log($"[ServerNetworkManager] NGO Server started on port {_port}");
        }

        private void OnClientConnected(ulong clientId)
        {
            Debug.Log($"[ServerNetworkManager] Client connected: {clientId}");
            _gameMode.OnClientConnected(clientId);
        }

        private void OnClientDisconnected(ulong clientId)
        {
            Debug.Log($"[ServerNetworkManager] Client disconnected: {clientId}");
            _gameMode.OnClientDisconnected(clientId);
        }

        private void OnDestroy()
        {
            _gameMode?.Cleanup();

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
