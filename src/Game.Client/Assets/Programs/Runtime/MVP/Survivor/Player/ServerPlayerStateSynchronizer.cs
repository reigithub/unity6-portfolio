using Game.Shared.Netcode.Survivor;
using Game.Shared.Survivor;

namespace Game.MVP.Survivor.Player
{
    /// <summary>
    /// Server/Host 用状態同期。NetworkSurvivorPlayerState に状態を反映。
    /// </summary>
    public class ServerPlayerStateSynchronizer : IPlayerStateSynchronizer
    {
        private readonly NetworkSurvivorPlayerState _networkPlayerState;

        public ServerPlayerStateSynchronizer(NetworkSurvivorPlayerState networkPlayerState)
        {
            _networkPlayerState = networkPlayerState;
        }

        public void PushState(NetworkSurvivorPlayerStateSnapshot snapshot)
        {
            _networkPlayerState.UpdateState(snapshot);
        }
    }
}
