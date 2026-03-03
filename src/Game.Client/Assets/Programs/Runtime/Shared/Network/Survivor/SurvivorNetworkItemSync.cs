using Game.Shared.Survivor;
using MessagePipe;
using Mirror;
using UnityEngine;
using VContainer;

namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// アイテム生成・取得のバッチ同期マネージャー（シングルトン）。
    /// アイテムのスポーン・デスポーンを ClientRpc で通知。
    /// </summary>
    public class SurvivorNetworkItemSync : NetworkBehaviour
    {
        public static SurvivorNetworkItemSync Instance { get; private set; }

        [Inject] private IPublisher<SurvivorSignals.Item.Spawned> _itemSpawnedPub;
        [Inject] private IPublisher<SurvivorSignals.Item.Despawned> _itemDespawnedPub;

        // --- アイテมスポーン ---

        [ClientRpc]
        public void SpawnItemClientRpc(int itemId, float posX, float posZ)
        {
            if (!isServer)
            {
                _itemSpawnedPub?.Publish(new SurvivorSignals.Item.Spawned(itemId, posX, posZ));
            }
        }

        // --- アイテム回収（NetworkSurvivorGameManager.NotifyItemCollectedClientRpc で通知） ---

        [ClientRpc]
        public void DespawnItemClientRpc(int itemId)
        {
            if (!isServer)
            {
                _itemDespawnedPub?.Publish(new SurvivorSignals.Item.Despawned(itemId));
            }
        }

        // --- ライフサイクル ---

        public override void OnStartServer()
        {
            Instance = this;
            Debug.Log("[NetworkSurvivorItemSync] Spawned on server");
        }

        public override void OnStartClient()
        {
            Instance = this;
            Debug.Log("[NetworkSurvivorItemSync] Spawned on client");
        }

        public override void OnStopServer()
        {
            if (Instance == this) Instance = null;
        }

        public override void OnStopClient()
        {
            if (Instance == this) Instance = null;
        }
    }
}
