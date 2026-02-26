using Unity.Netcode;
using UnityEngine;

namespace Game.Shared.Netcode.Survivor
{
    /// <summary>
    /// 敵状態のバッチ同期マネージャー（シングルトン NetworkBehaviour）。
    /// 個別敵を NetworkObject にせず、ClientRpc で配列一括送信する。
    /// Vampire Survivors 規模（数百体）に対応するためのバッチ管理型設計。
    /// </summary>
    public class NetworkSurvivorEnemyState : NetworkBehaviour
    {
        public static NetworkSurvivorEnemyState Instance { get; private set; }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                Instance = this;
            }
            Debug.Log($"[NetworkSurvivorEnemyState] Spawned (IsServer={IsServer})");
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }

        // --- サーバー → クライアント: バッチ送信 ---

        /// <summary>敵状態を一括同期（スポーン/位置更新/死亡）</summary>
        [ClientRpc]
        public void SyncEnemiesStateClientRpc(NetworkSurvivorEnemyStateSnapshot[] enemies)
        {
            // Phase 5+: クライアント側で EnemyView 更新
            // SyncType に応じて Spawn/PositionUpdate/Death を処理
        }

        // --- サーバー側ヘルパー ---

        /// <summary>サーバーから敵状態バッチを送信（Phase 4: SurvivorEnemySpawner 等から呼ばれる）</summary>
        public void BroadcastEnemyStates(NetworkSurvivorEnemyStateSnapshot[] snapshots)
        {
            if (!IsServer) return;
            SyncEnemiesStateClientRpc(snapshots);
        }
    }
}
