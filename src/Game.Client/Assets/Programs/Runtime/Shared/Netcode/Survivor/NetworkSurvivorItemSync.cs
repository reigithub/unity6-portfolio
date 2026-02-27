using MessagePipe;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace Game.Shared.Netcode.Survivor
{
    /// <summary>
    /// アイテム生成・取得のバッチ同期マネージャー（シングルトン）。
    /// アイテムのスポーン・デスポーンを ClientRpc で通知。
    /// </summary>
    public class NetworkSurvivorItemSync : NetworkBehaviour
    {
        public static NetworkSurvivorItemSync Instance { get; private set; }

        [Inject] private IPublisher<SurvivorSignals.Item.Spawned> _itemSpawnedPub;
        [Inject] private IPublisher<SurvivorSignals.Item.Despawned> _itemDespawnedPub;

        // --- アイテムスポーン ---

        [ClientRpc]
        public void SpawnItemClientRpc(int itemId, float posX, float posZ)
        {
            if (!IsServer)
            {
                _itemSpawnedPub?.Publish(new SurvivorSignals.Item.Spawned(itemId, posX, posZ));
            }
        }

        // --- アイテム回収（NetworkSurvivorGameManager.NotifyItemCollectedClientRpc で通知） ---

        [ClientRpc]
        public void DespawnItemClientRpc(int itemId)
        {
            if (!IsServer)
            {
                _itemDespawnedPub?.Publish(new SurvivorSignals.Item.Despawned(itemId));
            }
        }

        // --- ライフサイクル ---

        public override void OnNetworkSpawn()
        {
            Instance = this; // サーバー・クライアント両方で設定（InjectGameObject でアクセスするため）
            Debug.Log($"[NetworkSurvivorItemSync] Spawned (IsServer={IsServer})");
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }
    }
}
