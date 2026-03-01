namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// Server/Host 用ネットワークブリッジ。
    /// NGO の NetworkBehaviour singleton 経由でクライアントに状態を送信。
    /// </summary>
    public class SurvivorNetworkBridge : ISurvivorNetworkBridge
    {
        public void BroadcastEnemyStates(SurvivorNetworkEnemyStateSnapshot[] snapshots)
        {
            SurvivorNetworkEnemyState.Instance?.BroadcastEnemyStates(snapshots);
        }

        public void NotifyItemSpawned(int itemId, float posX, float posZ)
        {
            SurvivorNetworkItemSync.Instance?.SpawnItemClientRpc(itemId, posX, posZ);
        }

        public void NotifyItemDespawned(int itemId)
        {
            SurvivorNetworkItemSync.Instance?.DespawnItemClientRpc(itemId);
        }
    }
}
