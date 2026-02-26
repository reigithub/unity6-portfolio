using Unity.Netcode;
using UnityEngine;

namespace Game.Shared.Netcode.Survivor
{
    /// <summary>
    /// アイテム生成・取得のバッチ同期マネージャー（シングルトン）。
    /// アイテムのスポーン・デスポーンを ClientRpc で通知。
    /// </summary>
    public class NetworkSurvivorItemSync : NetworkBehaviour
    {
        public static NetworkSurvivorItemSync Instance { get; private set; }

        // --- アイテムスポーン ---

        [ClientRpc]
        public void SpawnItemClientRpc(int itemId, float posX, float posZ)
        {
            // Phase 5+: クライアント側でアイテムビジュアル生成
        }

        // --- アイテム回収（NetworkSurvivorGameManager.NotifyItemCollectedClientRpc で通知） ---

        [ClientRpc]
        public void DespawnItemClientRpc(int itemId)
        {
            // Phase 5+: クライアント側でアイテムビジュアル削除
        }

        // --- ライフサイクル ---

        public override void OnNetworkSpawn()
        {
            if (IsServer) Instance = this;
            Debug.Log($"[NetworkSurvivorItemSync] Spawned (IsServer={IsServer})");
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }
    }
}
