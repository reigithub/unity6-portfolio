using System;
using System.Threading.Tasks;
using Game.Library.Shared.Dto;

namespace Game.Shared.Realtime.Client
{
    /// <summary>
    /// ロビークライアントインターフェース（Unary + Hub ハイブリッド）
    /// </summary>
    public interface ILobbyClient : IDisposable
    {
        /// <summary>
        /// ロビーに接続済みかどうか
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 現在接続中のロビーID（未接続時は null）
        /// </summary>
        string CurrentLobbyId { get; }

        /// <summary>
        /// ロビー作成（Unary のみ）
        /// </summary>
        Task<CreateLobbyResponse> CreateLobbyAsync(CreateLobbyRequest request);

        /// <summary>
        /// ロビーに参加（Unary のみ）
        /// </summary>
        Task<LobbyInfo> JoinLobbyAsync(string lobbyId, string playerName);

        /// <summary>
        /// ロビーに Hub 接続（リアルタイムイベント受信開始）
        /// </summary>
        Task ConnectToLobbyAsync(string lobbyId, string playerName);

        /// <summary>
        /// ロビーから退出
        /// </summary>
        Task LeaveLobbyAsync();

        /// <summary>
        /// Hub 接続を切断しリソースを解放する（非同期）
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// ロビー検索（Unary のみ）
        /// </summary>
        Task<LobbyInfo[]> SearchLobbiesAsync(string gameMode, int maxResults);

        /// <summary>
        /// ロビー情報取得（Unary のみ）
        /// </summary>
        Task<LobbyInfo> GetLobbyInfoAsync(string lobbyId);

        /// <summary>
        /// ロビーのプレイヤー一覧取得（Unary のみ）
        /// </summary>
        Task<LobbyPlayerInfo[]> GetLobbyPlayersAsync(string lobbyId);

        /// <summary>
        /// メッセージ送信（Hub）
        /// </summary>
        Task SendMessageAsync(string message);

        /// <summary>
        /// レディ状態設定（Hub）
        /// </summary>
        Task SetReadyAsync(bool isReady);

        /// <summary>
        /// ステージ変更（Hub、ホストのみ）
        /// </summary>
        Task SetStageAsync(int stageId);

        /// <summary>
        /// 自分が参加中のロビーを取得（未参加時は null）
        /// </summary>
        Task<LobbyInfo> GetMyLobbyAsync();

        /// <summary>
        /// プレイヤー参加イベント
        /// </summary>
        event Action<string, string> OnPlayerJoined;

        /// <summary>
        /// プレイヤー退出イベント
        /// </summary>
        event Action<string, string> OnPlayerLeft;

        /// <summary>
        /// メッセージ受信イベント
        /// </summary>
        event Action<string, string, string> OnMessageReceived;

        /// <summary>
        /// レディ状態変更イベント
        /// </summary>
        event Action<string, bool> OnPlayerReadyChanged;

        /// <summary>
        /// ゲーム開始イベント (matchId, serverAddress, serverPort, sessionToken)
        /// </summary>
        event Action<string, string, int, string> OnGameStarting;

        /// <summary>
        /// ロビー閉鎖イベント (reason)
        /// </summary>
        event Action<string> OnLobbyClosed;

        /// <summary>
        /// ステージ変更イベント (stageId, changedByUserId)
        /// </summary>
        event Action<int, string> OnStageChanged;

        /// <summary>
        /// 予期しない切断イベント (reason)
        /// </summary>
        event Action<string> OnDisconnected;
    }
}
