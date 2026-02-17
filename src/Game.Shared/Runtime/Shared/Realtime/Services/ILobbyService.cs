using System.Threading.Tasks;
using Game.Library.Shared.Realtime.Dto;
using MagicOnion;

namespace Game.Library.Shared.Realtime.Services
{
    /// <summary>
    /// ロビー Unary RPC サービスインターフェース
    /// </summary>
    public interface ILobbyService : IService<ILobbyService>
    {
        /// <summary>
        /// ロビー作成
        /// </summary>
        UnaryResult<CreateLobbyResponse> CreateLobbyAsync(CreateLobbyRequest request);

        /// <summary>
        /// ロビーに参加
        /// </summary>
        UnaryResult<LobbyInfo> JoinLobbyAsync(string lobbyId, string playerName);

        /// <summary>
        /// ロビーから退出
        /// </summary>
        UnaryResult<bool> LeaveLobbyAsync(string lobbyId);

        /// <summary>
        /// ロビー検索
        /// </summary>
        UnaryResult<LobbyInfo[]> SearchLobbiesAsync(string gameMode, int maxResults);

        /// <summary>
        /// ロビー情報取得
        /// </summary>
        UnaryResult<LobbyInfo> GetLobbyInfoAsync(string lobbyId);

        /// <summary>
        /// ロビーのプレイヤー一覧を取得
        /// </summary>
        UnaryResult<LobbyPlayerInfo[]> GetLobbyPlayersAsync(string lobbyId);
    }
}
