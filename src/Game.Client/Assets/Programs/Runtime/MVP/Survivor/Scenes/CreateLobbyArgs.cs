using Game.Library.Shared.Dto;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// Create Lobby UI から Presenter に渡すパラメータ集約。
    /// 順序ミス防止のため named field の readonly struct とする。
    /// </summary>
    public readonly struct CreateLobbyArgs
    {
        public string LobbyName { get; }
        public int MaxPlayers { get; }
        public int StageId { get; }
        public NetworkTopology Topology { get; }

        public CreateLobbyArgs(string lobbyName, int maxPlayers, int stageId, NetworkTopology topology)
        {
            LobbyName = lobbyName;
            MaxPlayers = maxPlayers;
            StageId = stageId;
            Topology = topology;
        }
    }
}
