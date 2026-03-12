using Game.Shared.Network.Survivor;

namespace Game.Shared.Network.Fusion
{
    /// <summary>
    /// ISurvivorNetworkBridge の Fusion 実装。
    /// Host モード: SurvivorFusionGameState 経由で MessagePipe に直接 Publish。
    /// Client モード（将来）: Fusion RPC 経由で全クライアントにブロードキャスト。
    /// </summary>
    public class SurvivorFusionNetworkBridge : ISurvivorNetworkBridge
    {
        public void NotifyItemSpawned(int itemId, float posX, float posY, float posZ)
        {
            SurvivorFusionGameState.Instance?.NotifyItemSpawned(itemId, posX, posY, posZ);
        }

        public void NotifyItemDespawned(int itemId)
        {
            SurvivorFusionGameState.Instance?.NotifyItemDespawned(itemId);
        }
    }
}
