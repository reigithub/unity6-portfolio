using System.Collections.Concurrent;
using System.Threading;
using Game.Library.Shared.Realtime.Hubs;
using Game.Realtime.Services;
using Game.Realtime.Validation;
using Game.Server.Shared.Extensions;
using Grpc.Core;
using MagicOnion;
using MagicOnion.Server.Hubs;
using Microsoft.Extensions.Options;

namespace Game.Realtime.Hubs;

/// <summary>
/// ロビーHub サーバー実装
/// ロビー参加/退出は Unary ILobbyService 経由。Hub はリアルタイムイベント（チャット、レディ、ゲーム開始）専用。
/// </summary>
public class LobbyHub : StreamingHubBase<ILobbyHub, ILobbyHubReceiver>, ILobbyHub
{
    private readonly ILogger<LobbyHub> _logger;
    private readonly ILobbyDataService _lobbyDataService;
    private readonly IUnityServerAuthApiClient _unityServerAuthApi;
    private readonly UnityServerConfiguration _unityServerConfig;
    private readonly ILobbyValidator _lobbyValidator;

    // lobby ごとの userId → ConnectionId マッピング（Hub はリクエストごとにインスタンス生成のため static）
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Guid>> LobbyConnections = new();

    private IGroup<ILobbyHubReceiver>? _currentGroup;
    private string _userId = string.Empty;
    private string _playerName = string.Empty;
    private string _lobbyId = string.Empty;
    private int _hasLeft;

    public LobbyHub(
        ILogger<LobbyHub> logger,
        ILobbyDataService lobbyDataService,
        IUnityServerAuthApiClient unityServerAuthApi,
        IOptions<UnityServerConfiguration> unityServerConfig,
        ILobbyValidator lobbyValidator)
    {
        _logger = logger;
        _lobbyDataService = lobbyDataService;
        _unityServerAuthApi = unityServerAuthApi;
        _unityServerConfig = unityServerConfig.Value;
        _lobbyValidator = lobbyValidator;
    }

    public async ValueTask ConnectAsync(string lobbyId, string playerName)
    {
        _lobbyValidator.ValidateLobbyId(lobbyId);
        _lobbyValidator.ValidatePlayerName(playerName);

        _userId = Context.CallContext.GetHttpContext().User.GetRequiredUserId();
        _playerName = playerName;
        _lobbyId = lobbyId;

        // 再接続時にレディ状態をリセット
        await _lobbyDataService.SetReadyAsync(lobbyId, _userId, false);

        _currentGroup = await Group.AddAsync(lobbyId);

        // userId → ConnectionId マッピングを記録
        var lobbyMap = LobbyConnections.GetOrAdd(lobbyId, _ => new ConcurrentDictionary<string, Guid>());
        lobbyMap[_userId] = ConnectionId;

        _logger.LogInformation(
            "Player {PlayerName} ({UserId}) connected to lobby {LobbyId}",
            playerName, _userId, lobbyId);

        _currentGroup.All.OnPlayerJoined(_userId, playerName);
    }

    public async ValueTask LeaveAsync()
    {
        if (Interlocked.CompareExchange(ref _hasLeft, 1, 0) != 0)
            return;

        if (_currentGroup != null)
        {
            _logger.LogInformation(
                "Player {PlayerName} ({UserId}) left lobby {LobbyId}",
                _playerName, _userId, _lobbyId);

            _currentGroup.All.OnPlayerLeft(_userId, _playerName);

            // ホスト退出時はロビーを閉じる
            var lobby = await _lobbyDataService.GetLobbyAsync(_lobbyId);
            if (lobby != null && lobby.HostUserId == _userId)
            {
                _currentGroup.All.OnLobbyClosed("Host left");
            }

            await _currentGroup.RemoveAsync(Context);
            _currentGroup = null;

            if (!string.IsNullOrEmpty(_lobbyId))
            {
                if (LobbyConnections.TryGetValue(_lobbyId, out var lobbyMap))
                    lobbyMap.TryRemove(_userId, out _);
                await _lobbyDataService.RemovePlayerAsync(_lobbyId, _userId);
            }
        }
    }

    public ValueTask SendMessageAsync(string message)
    {
        _lobbyValidator.ValidateLobbyMessage(message);

        if (_currentGroup != null)
        {
            _logger.LogDebug("Player {PlayerName} sent message in lobby {LobbyId}", _playerName, _lobbyId);
            _currentGroup.All.OnMessageReceived(_userId, _playerName, message);
        }

        return default;
    }

    public async ValueTask SetStageAsync(int stageId)
    {
        if (string.IsNullOrEmpty(_lobbyId)) return;

        // ホストのみ変更可能
        var lobby = await _lobbyDataService.GetLobbyAsync(_lobbyId);
        if (lobby == null || lobby.HostUserId != _userId)
        {
            throw new ReturnStatusException(StatusCode.PermissionDenied, "Only the host can change the stage");
        }

        await _lobbyDataService.SetStageAsync(_lobbyId, stageId);

        _currentGroup?.All.OnStageChanged(stageId, _userId);

        _logger.LogInformation(
            "Host {UserId} changed stage to {StageId} in lobby {LobbyId}",
            _userId, stageId, _lobbyId);
    }

    public async ValueTask SetReadyAsync(bool isReady)
    {
        if (string.IsNullOrEmpty(_lobbyId)) return;

        var (success, allReady) = await _lobbyDataService.SetReadyAndCheckAllAsync(_lobbyId, _userId, isReady);
        if (!success) return;

        if (_currentGroup != null)
        {
            _currentGroup.All.OnPlayerReadyChanged(_userId, isReady);
        }

        // 全員 Ready チェック → ゲーム開始（SetReady と AllReady はアトミック）
        if (isReady && allReady && _currentGroup != null)
        {
            await StartGameAsync();
        }

        _logger.LogDebug(
            "Player {UserId} set ready={IsReady} in lobby {LobbyId}",
            _userId, isReady, _lobbyId);
    }

    private async ValueTask StartGameAsync()
    {
        var players = await _lobbyDataService.GetPlayersAsync(_lobbyId);
        if (players.Length == 0 || _currentGroup == null)
        {
            _logger.LogWarning("StartGameAsync aborted: lobby {LobbyId} has no players or group is null", _lobbyId);
            return;
        }

        var matchId = $"mp-{Guid.NewGuid():N}";

        // 全プレイヤーに同一 matchId でトークンを発行して送信
        LobbyConnections.TryGetValue(_lobbyId, out var lobbyMap);
        foreach (var player in players)
        {
            var authResponse = await _unityServerAuthApi.IssueTokenAsync(player.UserId, matchId);

            if (lobbyMap != null && lobbyMap.TryGetValue(player.UserId, out var connId))
            {
                _currentGroup.Only(new[] { connId }).OnGameStarting(
                    matchId,
                    _unityServerConfig.ServerAddress,
                    _unityServerConfig.ServerPort,
                    authResponse.Token);
            }
        }

        _logger.LogInformation(
            "Game starting from lobby {LobbyId}: match {MatchId} with {PlayerCount} players",
            _lobbyId, matchId, players.Length);
    }

    protected override async ValueTask OnDisconnected()
    {
        if (Interlocked.CompareExchange(ref _hasLeft, 1, 0) != 0)
        {
            _logger.LogDebug(
                "Player {PlayerName} ({UserId}) already left lobby {LobbyId}, skipping OnDisconnected cleanup",
                _playerName, _userId, _lobbyId);
            return;
        }

        if (_currentGroup != null)
        {
            _currentGroup.All.OnPlayerLeft(_userId, _playerName);

            // ホスト退出時はロビーを閉じる
            var lobby = await _lobbyDataService.GetLobbyAsync(_lobbyId);
            if (lobby != null && lobby.HostUserId == _userId)
            {
                _currentGroup.All.OnLobbyClosed("Host disconnected");
            }

            await _currentGroup.RemoveAsync(Context);
            _currentGroup = null;
        }

        if (!string.IsNullOrEmpty(_lobbyId))
        {
            if (LobbyConnections.TryGetValue(_lobbyId, out var lobbyMap))
                lobbyMap.TryRemove(_userId, out _);
            await _lobbyDataService.RemovePlayerAsync(_lobbyId, _userId);
        }

        _logger.LogInformation(
            "Player {PlayerName} ({UserId}) disconnected from lobby {LobbyId}",
            _playerName, _userId, _lobbyId);
    }
}
