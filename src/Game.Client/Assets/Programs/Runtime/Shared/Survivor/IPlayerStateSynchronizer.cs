using Game.Shared.Netcode.Survivor;

namespace Game.Shared.Survivor
{
    /// <summary>
    /// プレイヤー状態のネットワーク同期の抽象化。
    /// - Server/Host: ServerPlayerStateSynchronizer（NetworkSurvivorPlayerState へ状態を送信）
    /// - SP/Client: null（同期不要）
    /// </summary>
    public interface IPlayerStateSynchronizer
    {
        /// <summary>
        /// プレイヤー状態をネットワークに反映する。
        /// Controller が構築したスナップショットを受け取り、ネットワーク層へ転送。
        /// </summary>
        void PushState(NetworkSurvivorPlayerStateSnapshot snapshot);
    }
}
