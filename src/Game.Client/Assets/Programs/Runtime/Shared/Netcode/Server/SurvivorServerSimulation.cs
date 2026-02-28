#if UNITY_SERVER
using Cysharp.Threading.Tasks;
using Game.Shared.Netcode.Survivor;
using UnityEngine;

namespace Game.Shared.Netcode.Server
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

        /// <summary>
        /// サーバー起動時の初期化。stageId はクライアント接続時に受信する。
        /// </summary>
        public void Initialize()
        {
            _stageLoaded = false;
            Debug.Log("[SurvivorServerSimulation] Initialized (waiting for client stageId)");
        }

        /// <summary>
        /// クライアントの ConnectionData から受信した stageId を設定する。
        /// 初回クライアントの値で確定し、2人目以降は一致チェックのみ。
        /// </summary>
        public void SetStageIdFromClient(int stageId)
        {
            if (_stageLoaded)
            {
                if (_stageId != stageId)
                {
                    Debug.LogWarning($"[SurvivorServerSimulation] StageId mismatch: expected={_stageId}, received={stageId}");
                }
                return;
            }
            _stageId = stageId;
            _stageLoaded = true;
            Debug.Log($"[SurvivorServerSimulation] Stage set to {stageId} from client");
        }

        /// <summary>初回クライアント接続でセッション開始</summary>
        public void OnFirstClientConnected()
        {
            if (_sessionStarted || !_stageLoaded) return;
            _sessionStarted = true;
            ServerNetworkManager.Instance.StartSession();
            Debug.Log("[SurvivorServerSimulation] Session started");

            // シングルトン OnNetworkSpawn 完了を待機してから通知
            NotifyPlayersReadyAsync().Forget();
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
                Debug.Log("[SurvivorServerSimulation] AllPlayersReady + GameStarted sent");
            }
            else
            {
                Debug.LogWarning("[SurvivorServerSimulation] NetworkSurvivorGameManager not found");
            }
        }

        public void EndSession()
        {
            _sessionStarted = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
#endif
