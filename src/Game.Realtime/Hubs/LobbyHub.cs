using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Game.Library.Shared.Dto;
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
    private readonly IUnityServerApiClient _unityServerApi;
    private readonly UnityServerConfiguration _unityServerConfig;
    private readonly ILobbyValidator _lobbyValidator;

    // lobby ごとの userId → ConnectionId マッピング（Hub はリクエストごとにインスタンス生成のため static）
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Guid>> LobbyConnections = new();

    // P2P 開始フロー: lobbyId → Host の準備完了を待つ TCS。
    // StartP2PGameAsync が Host 先行 broadcast 後にこの TCS を await し、
    // NotifyHostReadyAsync (Host 側 RPC) または Host disconnect / タイムアウトで完了する。
    private static readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pendingHostReady = new();

    // P2P 開始フロー: lobbyId → 既に Lobby 閉鎖系 broadcast を発火済みかのフラグ。
    // OnDisconnected 経路 (OnLobbyClosed("Host disconnected")) と StartP2PGameAsync タイムアウト経路
    // (OnLobbyClosed("Host failed...")) の二重 broadcast を防ぐ。
    private static readonly ConcurrentDictionary<string, byte> _lobbyClosedBroadcasted = new();

    private IGroup<ILobbyHubReceiver>? _currentGroup;
    private string _userId = string.Empty;
    private string _playerName = string.Empty;
    private string _lobbyId = string.Empty;
    private int _hasLeft;

    public LobbyHub(
        ILogger<LobbyHub> logger,
        ILobbyDataService lobbyDataService,
        IUnityServerApiClient unityServerApi,
        IOptions<UnityServerConfiguration> unityServerConfig,
        ILobbyValidator lobbyValidator)
    {
        _logger = logger;
        _lobbyDataService = lobbyDataService;
        _unityServerApi = unityServerApi;
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
            bool isHost = lobby != null && lobby.HostUserId == _userId;
            if (isHost)
            {
                // P2P 開始フローの Host 待機 TCS を取り消す + 二重 broadcast 防止フラグを立てる。
                if (_pendingHostReady.TryRemove(_lobbyId, out var tcsOnLeave))
                {
                    tcsOnLeave.TrySetResult(false);
                }
                _lobbyClosedBroadcasted.TryAdd(_lobbyId, 1);

                _currentGroup.All.OnLobbyClosed("Host left");
            }

            await _currentGroup.RemoveAsync(Context);
            _currentGroup = null;

            if (!string.IsNullOrEmpty(_lobbyId))
            {
                if (LobbyConnections.TryGetValue(_lobbyId, out var lobbyMap))
                    lobbyMap.TryRemove(_userId, out _);

                if (isHost)
                {
                    // ホスト退出時はロビーごと削除 (残プレイヤー有無に関わらず)。
                    // RemovePlayerAsync は残プレイヤー 0 のときだけ削除するため、
                    // ホスト退出を検知したら明示的に DeleteAsync を呼ぶ必要がある。
                    await _lobbyDataService.DeleteAsync(_lobbyId);
                }
                else
                {
                    await _lobbyDataService.RemovePlayerAsync(_lobbyId, _userId);
                }
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
        var lobby = await _lobbyDataService.GetLobbyAsync(_lobbyId);
        if (players.Length == 0 || lobby == null || _currentGroup == null)
        {
            _logger.LogWarning("StartGameAsync aborted: lobby {LobbyId} has no players or group/lobby is null", _lobbyId);
            return;
        }

        LobbyConnections.TryGetValue(_lobbyId, out var lobbyMap);

        // ゲーム開始時の Ready 状態リセット: P2P 経路では StartP2PGameAsync 内で Host 完了を最大 20s
        // 待機する可能性があるため、Ready リセットは Host 先行 broadcast の前に完了させて UI ジッタを避ける。
        await _lobbyDataService.ResetAllReadyAsync(_lobbyId);
        foreach (var player in players)
        {
            _currentGroup.All.OnPlayerReadyChanged(player.UserId, false);
        }

        if (lobby.NetworkTopology == NetworkTopology.PeerToPeer)
        {
            _ = ExecuteP2PStartAsync(players, lobby, lobbyMap);
        }
        else
        {
            await StartDsGameAsync(players, lobby, lobbyMap);
        }
    }

    private async Task ExecuteP2PStartAsync(LobbyPlayerInfo[] players, LobbyInfo lobby, ConcurrentDictionary<string, Guid>? lobbyMap)
    {
        try
        {
            await StartP2PGameAsync(players, lobby, lobbyMap);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StartP2PGameAsync failed for lobby {LobbyId}", _lobbyId);
        }
    }

    private async Task StartDsGameAsync(LobbyPlayerInfo[] players, LobbyInfo lobby, ConcurrentDictionary<string, Guid>? lobbyMap)
    {
        var matchId = $"mp-{Guid.NewGuid():N}";
        var stageId = lobby.StageId;

        // リーダー（先頭プレイヤー）のトークン発行時に DS セッション割り当てを実行
        var isFirst = true;
        foreach (var player in players)
        {
            var authResponse = await _unityServerApi.IssueTokenAsync(
                player.UserId,
                sessionName: matchId,
                stageId: isFirst ? stageId : 0,
                playerCount: players.Length,
                hostUserId: lobby.HostUserId);
            isFirst = false;

            if (lobbyMap != null && lobbyMap.TryGetValue(player.UserId, out var connId))
            {
                var info = new MatchStartInfo
                {
                    Topology = NetworkTopology.DedicatedServer,
                    SessionName = matchId,
                    ServerAddress = _unityServerConfig.ServerAddress,
                    ServerPort = _unityServerConfig.ServerPort,
                    SessionToken = authResponse.Token,
                    PlayerCount = players.Length,
                    HostUserId = lobby.HostUserId,
                };
                _currentGroup!.Only(new[] { connId }).OnGameStarting(info);
            }
        }

        _logger.LogInformation(
            "DS game starting from lobby {LobbyId}: match {MatchId} with {PlayerCount} players",
            _lobbyId, matchId, players.Length);
    }

    /// <summary>
    /// P2P モードのゲーム開始フロー。Host 先行起動方式で Photon Cloud のセッション作成競合
    /// (Client が Host より先に StartClientAsync を呼んで GameNotFound になる) を防ぐ。
    /// 順序: ① Host にだけ OnGameStarting → ② Host の NotifyHostReadyAsync を await (timeout 20s)
    ///       → ③ 残りクライアントに OnGameStarting broadcast。
    /// </summary>
    private async Task StartP2PGameAsync(LobbyPlayerInfo[] players, LobbyInfo lobby, ConcurrentDictionary<string, Guid>? lobbyMap)
    {
        // [Photon 制約] 1 セッションに Host は 1 名のみ。本実装ではロビーホスト = Photon Host で固定。
        var hostUserId = lobby.HostUserId;
        if (string.IsNullOrEmpty(hostUserId))
        {
            _logger.LogWarning("StartP2PGameAsync aborted: lobby {LobbyId} has empty HostUserId", _lobbyId);
            return;
        }

        if (lobbyMap == null || !lobbyMap.TryGetValue(hostUserId, out var hostConnId))
        {
            _logger.LogWarning("StartP2PGameAsync aborted: host connection not found in lobby {LobbyId}", _lobbyId);
            return;
        }

        var sessionName = $"p2p-{Guid.NewGuid():N}";  // 36 文字、Photon SessionName 制限 64 内
        var photonRegion = "jp";                      // 将来的に LobbyInfo.PhotonRegion フィールド追加 + UI 選択
        var info = new MatchStartInfo
        {
            Topology = NetworkTopology.PeerToPeer,
            SessionName = sessionName,
            PhotonRegion = photonRegion,
            HostUserId = hostUserId,
            PlayerCount = players.Length,
        };

        // ① Host にだけ先に broadcast。
        if (_currentGroup == null) return;
        _currentGroup.Only(new[] { hostConnId }).OnGameStarting(info);

        // ② Host の準備完了 (NotifyHostReadyAsync) を待つ。
        // タイムアウト 20s: 正常系では Photon Cloud StartGame + GameState Spawn まで数秒で完了するため
        // 異常系の上限として十分。これを超える場合はフロー異常と見なす。
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingHostReady[_lobbyId] = tcs;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var ctsRegistration = cts.Token.Register(() => tcs.TrySetResult(false));

        bool hostReady;
        try
        {
            hostReady = await tcs.Task;
        }
        finally
        {
            _pendingHostReady.TryRemove(_lobbyId, out _);
        }

        if (!hostReady)
        {
            _logger.LogWarning(
                "P2P host {HostUserId} failed to be ready within timeout, lobby {LobbyId}",
                hostUserId, _lobbyId);

            // OnDisconnected 経路 (OnLobbyClosed("Host disconnected")) で既に broadcast 済みの場合は二重発火回避。
            if (_lobbyClosedBroadcasted.TryAdd(_lobbyId, 1) && _currentGroup != null)
            {
                _currentGroup.All.OnLobbyClosed("Host failed to start the session");
            }

            // Lobby 物理削除: Host が生存したまま起動失敗するケースで Redis にロビーが残るのを防ぐ。
            try
            {
                await _lobbyDataService.DeleteAsync(_lobbyId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete lobby {LobbyId} after host startup timeout", _lobbyId);
            }
            return;
        }

        // ③ 残りクライアントに broadcast (Host を除外)。
        if (_currentGroup == null) return;
        foreach (var player in players)
        {
            if (player.UserId == hostUserId) continue;
            if (lobbyMap.TryGetValue(player.UserId, out var connId))
            {
                _currentGroup.Only(new[] { connId }).OnGameStarting(info);
            }
        }

        _logger.LogInformation(
            "P2P game starting from lobby {LobbyId}: session {SessionName}, host {HostUserId}, region {Region}, players {Count}",
            _lobbyId, sessionName, hostUserId, photonRegion, players.Length);
    }

    public async ValueTask NotifyHostReadyAsync()
    {
        if (string.IsNullOrEmpty(_lobbyId)) return;

        // ホスト権限チェック: 呼出者が Lobby Host でなければ拒否。
        var lobby = await _lobbyDataService.GetLobbyAsync(_lobbyId);
        if (lobby == null || lobby.HostUserId != _userId)
        {
            throw new ReturnStatusException(StatusCode.PermissionDenied,
                "Only the host can call NotifyHostReadyAsync");
        }

        if (_pendingHostReady.TryGetValue(_lobbyId, out var tcs))
        {
            tcs.TrySetResult(true);
            _logger.LogDebug("Host {HostUserId} signalled ready for lobby {LobbyId}", _userId, _lobbyId);
        }
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

        bool isHost = false;

        if (_currentGroup != null)
        {
            _currentGroup.All.OnPlayerLeft(_userId, _playerName);

            // ホスト退出時はロビーを閉じる
            var lobby = await _lobbyDataService.GetLobbyAsync(_lobbyId);
            isHost = lobby != null && lobby.HostUserId == _userId;
            if (isHost)
            {
                // P2P 開始フローの Host 待機 TCS を取り消す + 二重 broadcast 防止フラグを立てる。
                if (_pendingHostReady.TryRemove(_lobbyId, out var tcsOnDisconnect))
                {
                    tcsOnDisconnect.TrySetResult(false);
                }
                _lobbyClosedBroadcasted.TryAdd(_lobbyId, 1);

                _currentGroup.All.OnLobbyClosed("Host disconnected");
            }

            await _currentGroup.RemoveAsync(Context);
            _currentGroup = null;
        }

        if (!string.IsNullOrEmpty(_lobbyId))
        {
            if (LobbyConnections.TryGetValue(_lobbyId, out var lobbyMap))
                lobbyMap.TryRemove(_userId, out _);

            if (isHost)
            {
                // ホスト切断時はロビーごと削除 (残プレイヤー有無に関わらず)
                await _lobbyDataService.DeleteAsync(_lobbyId);
            }
            else
            {
                await _lobbyDataService.RemovePlayerAsync(_lobbyId, _userId);
            }
        }

        _logger.LogInformation(
            "Player {PlayerName} ({UserId}) disconnected from lobby {LobbyId}",
            _playerName, _userId, _lobbyId);
    }
}
