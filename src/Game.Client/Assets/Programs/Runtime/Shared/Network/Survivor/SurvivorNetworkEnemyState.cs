using Game.Shared.Survivor;
using MessagePipe;
using Mirror;
using UnityEngine;
using VContainer;

namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// 敵状態のバッチ同期マネージャー（シングルトン NetworkBehaviour）。
    /// 個別敵を NetworkObject にせず、ClientRpc で配列一括送信する。
    /// Vampire Survivors 規模（数百体）に対応するためのバッチ管理型設計。
    /// </summary>
    public class SurvivorNetworkEnemyState : NetworkBehaviour
    {
        public static SurvivorNetworkEnemyState Instance { get; private set; }

        [Inject] private IPublisher<SurvivorSignals.Enemy.BatchUpdated> _enemyBatchPub;

        public override void OnStartServer()
        {
            Instance = this;
            Debug.Log("[NetworkSurvivorEnemyState] Spawned on server");
        }

        public override void OnStartClient()
        {
            Instance = this;
            Debug.Log("[NetworkSurvivorEnemyState] Spawned on client");
        }

        public override void OnStopServer()
        {
            if (Instance == this) Instance = null;
        }

        public override void OnStopClient()
        {
            if (Instance == this) Instance = null;
        }

        // --- サーバー → クライアント: バッチ送信 ---

        /// <summary>敵状態を一括同期（スポーン/位置更新/死亡）</summary>
        [ClientRpc]
        public void SyncEnemiesStateClientRpc(SurvivorNetworkEnemyStateSnapshot[] enemies)
        {
            if (!isServer)
            {
                _enemyBatchPub?.Publish(new SurvivorSignals.Enemy.BatchUpdated(enemies));
            }
        }

        // --- サーバー側ヘルパー ---

        /// <summary>サーバーから敵状態バッチを送信（Phase 4: SurvivorEnemySpawner 等から呼ばれる）</summary>
        public void BroadcastEnemyStates(SurvivorNetworkEnemyStateSnapshot[] snapshots)
        {
            if (!isServer) return;
            SyncEnemiesStateClientRpc(snapshots);
        }
    }
}
