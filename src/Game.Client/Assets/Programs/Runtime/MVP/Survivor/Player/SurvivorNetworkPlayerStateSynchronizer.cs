using Game.Shared.Network.Survivor;

namespace Game.MVP.Survivor.Player
{
    /// <summary>
    /// Server/Host 用状態同期。NetworkSurvivorPlayerState に状態を反映。
    /// </summary>
    public class SurvivorNetworkPlayerStateSynchronizer : ISurvivorNetworkPlayerStateSynchronizer
    {
        private readonly SurvivorNetworkPlayerState _networkPlayerState;

        public SurvivorNetworkPlayerStateSynchronizer(SurvivorNetworkPlayerState networkPlayerState)
        {
            _networkPlayerState = networkPlayerState;
        }

        public void PushState(SurvivorNetworkPlayerStateSnapshot snapshot)
        {
            _networkPlayerState.UpdateState(snapshot);
        }
    }
}
