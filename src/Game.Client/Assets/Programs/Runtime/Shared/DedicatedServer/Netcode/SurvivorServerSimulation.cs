#if UNITY_SERVER
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Shared.DedicatedServer.Netcode
{
    /// <summary>
    /// サーバー側セッションライフサイクル管理。
    /// ステージロードとセッション開始を制御する。
    /// </summary>
    public class SurvivorServerSimulation : MonoBehaviour
    {
        public static SurvivorServerSimulation Instance { get; private set; }

        private int _stageId;
        private bool _sessionStarted;
        private bool _stageLoaded;

        private void Awake() => Instance = this;

        public async UniTask InitializeAsync(int stageId)
        {
            _stageId = stageId;
            var sceneName = $"SurvivorStage_{stageId}";

            Debug.Log($"[SurvivorServerSimulation] Loading stage: {sceneName}");
            await SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            _stageLoaded = true;
            Debug.Log($"[SurvivorServerSimulation] Stage loaded: {sceneName}");
        }

        /// <summary>初回クライアント接続でセッション開始</summary>
        public void OnFirstClientConnected()
        {
            if (_sessionStarted || !_stageLoaded) return;
            _sessionStarted = true;
            ServerNetworkManager.Instance.StartSession();
            Debug.Log("[SurvivorServerSimulation] Session started");
        }

        public void EndSession()
        {
            _sessionStarted = false;
        }

        /// <summary>コマンドライン引数から --stage を解析。デフォルト 1。</summary>
        public static int ParseStageId()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--stage" && int.TryParse(args[i + 1], out int id))
                {
                    return id;
                }
            }
            return 1;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
#endif
