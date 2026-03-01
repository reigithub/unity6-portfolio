using Game.Shared.Network.Survivor;
using Game.Shared.Survivor;

namespace Game.MVP.Survivor.Player
{
    /// <summary>
    /// Server/Host 用状態同期。NetworkSurvivorPlayerState に状態を反映。
    /// </summary>
    public class ServerPlayerStateSynchronizer : IPlayerStateSynchronizer
    {
        private readonly SurvivorNetworkPlayerState _networkPlayerState;

        public ServerPlayerStateSynchronizer(SurvivorNetworkPlayerState networkPlayerState)
        {
            _networkPlayerState = networkPlayerState;
        }

        public void PushState(SurvivorNetworkPlayerStateSnapshot snapshot)
        {
            _networkPlayerState.UpdateState(snapshot);
        }
    }
}
