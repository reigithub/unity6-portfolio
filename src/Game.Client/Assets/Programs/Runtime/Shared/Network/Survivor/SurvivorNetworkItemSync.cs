using Game.Shared.Signals.Survivor;
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
        public void SpawnItemClientRpc(int itemId, float posX, float posY, float posZ)
        {
            Debug.Log($"[NetworkSurvivorItemSync] SpawnItem RPC: itemId={itemId}, pos=({posX},{posY},{posZ})");
            _itemSpawnedPub?.Publish(new SurvivorSignals.Item.Spawned(itemId, posX, posY, posZ));
        }

        // --- アイテム回収（NetworkSurvivorGameManager.NotifyItemCollectedClientRpc で通知） ---

        [ClientRpc]
        public void DespawnItemClientRpc(int itemId)
        {
            Debug.Log($"[NetworkSurvivorItemSync] DespawnItem RPC: itemId={itemId}");
            _itemDespawnedPub?.Publish(new SurvivorSignals.Item.Despawned(itemId));
        }

        // --- ライフサイクル ---

        public override void OnStartServer()
        {
            Instance = this;
            Debug.Log("[NetworkSurvivorItemSync] Spawned on server");
        }

        public override void OnStartClient()
        {
            DontDestroyOnLoad(gameObject);
            Instance = this;
            if (_itemSpawnedPub == null || _itemDespawnedPub == null)
                Debug.LogWarning($"[NetworkSurvivorItemSync] NULL publishers: spawned={_itemSpawnedPub != null}, despawned={_itemDespawnedPub != null}");
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
