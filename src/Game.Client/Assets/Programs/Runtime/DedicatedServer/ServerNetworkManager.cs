using System;
using Cysharp.Threading.Tasks;
using Game.Shared.Network.Survivor;
using kcp2k;
using Mirror;
using UnityEngine;

namespace Game.Shared.Netcode.Server
{
    /// <summary>
    /// Dedicated Server の Mirror インフラ管理。
    /// NetworkManager + KcpTransport を生成し、サーバーモードで起動する。
    /// ゲームタイプ固有ロジックは各セッションコンポーネントが独立して担当する。
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
        /// コンポーネント追加後、次フレームまで待機して内部初期化を完了させてから StartServer する。
        /// </summary>
        public void Initialize(ushort port)
        {
            _port = port;
            InitializeAsync().Forget();
        }

        private async UniTaskVoid InitializeAsync()
        {
            // --- KcpTransport セットアップ ---
            var transport = gameObject.AddComponent<KcpTransport>();
            transport.port = _port;
            Transport.active = transport;

            // --- NetworkManager セットアップ ---
            var nm = gameObject.AddComponent<NetworkManager>();
            nm.transport = transport;

            // --- Authenticator セットアップ ---
            var auth = gameObject.AddComponent<SurvivorNetworkAuthenticator>();
            nm.authenticator = auth;

            // --- スポーンプレハブ登録 ---
            await RegisterSpawnPrefabsAsync(nm);

            // NetworkManager の内部初期化を待機
            await UniTask.NextFrame();

            // --- サーバー起動 ---
            nm.StartServer();

            Debug.Log($"[ServerNetworkManager] Mirror Server started on port {_port}");
        }

        private static async UniTask RegisterSpawnPrefabsAsync(NetworkManager nm)
        {
            var registry = await SurvivorNetworkPrefabs.LoadAsync();
            if (registry == null || registry.Prefabs == null)
            {
                Debug.LogWarning("[ServerNetworkManager] SurvivorNetworkPrefabs not found");
                return;
            }

            foreach (var prefab in registry.Prefabs)
            {
                if (prefab != null)
                    nm.spawnPrefabs.Add(prefab);
            }

            Debug.Log($"[ServerNetworkManager] Registered {nm.spawnPrefabs.Count} spawn prefabs");
        }

        private void OnDestroy()
        {
            if (NetworkServer.active)
            {
                NetworkManager.singleton?.StopServer();
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
