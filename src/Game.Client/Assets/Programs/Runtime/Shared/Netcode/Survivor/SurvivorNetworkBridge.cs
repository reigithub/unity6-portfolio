namespace Game.Shared.Netcode.Survivor
{
    /// <summary>
    /// Server/Host 用ネットワークブリッジ。
    /// NGO の NetworkBehaviour singleton 経由でクライアントに状態を送信。
    /// </summary>
    public class SurvivorNetworkBridge : ISurvivorNetworkBridge
    {
        public void BroadcastEnemyStates(NetworkSurvivorEnemyStateSnapshot[] snapshots)
        {
            NetworkSurvivorEnemyState.Instance?.BroadcastEnemyStates(snapshots);
        }

        public void NotifyItemSpawned(int itemId, float posX, float posZ)
        {
            NetworkSurvivorItemSync.Instance?.SpawnItemClientRpc(itemId, posX, posZ);
        }

        public void NotifyItemDespawned(int itemId)
        {
            NetworkSurvivorItemSync.Instance?.DespawnItemClientRpc(itemId);
        }
    }
}
