namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// Spawner からネットワーク層への通知の抽象化。
    /// - Server/Host: ServerNetworkBridge（ClientRpc singleton 経由でクライアントに通知）
    /// - SP/Client: null（通知不要）
    /// </summary>
    public interface ISurvivorNetworkBridge
    {
        void NotifyItemSpawned(int itemId, float posX, float posY, float posZ);
        void NotifyItemDespawned(int itemId);
    }
}
