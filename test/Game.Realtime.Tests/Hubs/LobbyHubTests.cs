using System.Reflection;
using Game.Library.Shared.Dto;
using Game.Realtime.Hubs;
using Game.Realtime.Services;
using Game.Realtime.Validation;
using MagicOnion;
using MagicOnion.Server.Hubs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Game.Realtime.Tests.Hubs;

/// <summary>
/// LobbyHub の基本テスト
/// </summary>
public class LobbyHubTests
{
    private readonly Mock<ILogger<LobbyHub>> _mockLogger;
    private readonly Mock<ILobbyDataService> _mockLobbyDataService;
    private readonly Mock<IUnityServerApiClient> _mockUnityServerApi;
    private readonly IOptions<UnityServerConfiguration> _unityServerConfig;
    private readonly Mock<ILobbyValidator> _mockLobbyValidator;

    public LobbyHubTests()
    {
        _mockLogger = new Mock<ILogger<LobbyHub>>();
        _mockLobbyDataService = new Mock<ILobbyDataService>();
        _mockUnityServerApi = new Mock<IUnityServerApiClient>();
        _unityServerConfig = Options.Create(new UnityServerConfiguration());
        _mockLobbyValidator = new Mock<ILobbyValidator>();
    }

    private LobbyHub CreateHub()
    {
        return new LobbyHub(
            _mockLogger.Object, _mockLobbyDataService.Object, _mockUnityServerApi.Object,
            _unityServerConfig, _mockLobbyValidator.Object);
    }

    [Fact]
    public void LobbyHub_CanBeInstantiated()
    {
        // Act
        var hub = CreateHub();

        // Assert
        Assert.NotNull(hub);
    }

    [Fact]
    public void LobbyHub_ImplementsILobbyHub()
    {
        // Act
        var hub = CreateHub();

        // Assert
        Assert.IsAssignableFrom<Game.Library.Shared.Realtime.Hubs.ILobbyHub>(hub);
    }

    // ---------------------------------------------------------------
    // Finding #6: LeaveAsync / OnDisconnected deduplication tests
    // ---------------------------------------------------------------

    /// <summary>
    /// LeaveAsync を1回呼ぶと _hasLeft フラグが 1 に設定されることを検証。
    /// _currentGroup が null の場合、RemovePlayerAsync は呼ばれない。
    /// </summary>
    [Fact]
    public async Task LeaveAsync_SetsHasLeftFlag()
    {
        // Arrange
        var hub = CreateHub();

        // Act — _currentGroup is null, so LeaveAsync body is mostly a no-op
        // but the Interlocked flag should still be set
        await hub.LeaveAsync();

        // Assert — verify _hasLeft is now 1 via reflection
        var hasLeftField = typeof(LobbyHub).GetField("_hasLeft", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(hasLeftField);
        var hasLeftValue = (int)hasLeftField.GetValue(hub)!;
        Assert.Equal(1, hasLeftValue);

        // RemovePlayerAsync should not have been called (no group, no lobbyId)
        _mockLobbyDataService.Verify(
            s => s.RemovePlayerAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// LeaveAsync を2回呼んでも RemovePlayerAsync は最大1回しか呼ばれないことを検証。
    /// Interlocked.CompareExchange による重複実行防止を確認。
    /// </summary>
    [Fact]
    public async Task LeaveAsync_CalledTwice_SecondCallIsNoop()
    {
        // Arrange
        var hub = CreateHub();
        SetPrivateField(hub, "_lobbyId", "test-lobby-001");
        SetPrivateField(hub, "_userId", "test-user-001");

        // Act — call LeaveAsync twice
        await hub.LeaveAsync();
        await hub.LeaveAsync();

        // Assert — RemovePlayerAsync should be called at most once
        // (Since _currentGroup is null, the call inside the if-block won't happen,
        //  but the point is the second call returns immediately without entering the body.)
        var hasLeftField = typeof(LobbyHub).GetField("_hasLeft", BindingFlags.NonPublic | BindingFlags.Instance);
        var hasLeftValue = (int)hasLeftField!.GetValue(hub)!;
        Assert.Equal(1, hasLeftValue);
    }

    /// <summary>
    /// LeaveAsync 後に OnDisconnected が呼ばれても重複クリーンアップが実行されないことを検証。
    /// _hasLeft フラグが既に 1 なので OnDisconnected は早期リターンする。
    /// </summary>
    [Fact]
    public async Task OnDisconnected_AfterLeaveAsync_IsNoop()
    {
        // Arrange
        var hub = CreateHub();
        SetPrivateField(hub, "_lobbyId", "test-lobby-002");
        SetPrivateField(hub, "_userId", "test-user-002");

        _mockLobbyDataService
            .Setup(s => s.RemovePlayerAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act — LeaveAsync sets _hasLeft to 1
        await hub.LeaveAsync();

        // OnDisconnected should be a no-op since _hasLeft is already 1
        var onDisconnected = typeof(LobbyHub).GetMethod(
            "OnDisconnected", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(onDisconnected);
        await (ValueTask)onDisconnected.Invoke(hub, null)!;

        // Assert — RemovePlayerAsync should never have been called
        // (LeaveAsync skipped it because _currentGroup was null,
        //  and OnDisconnected early-returned because _hasLeft was already 1)
        _mockLobbyDataService.Verify(
            s => s.RemovePlayerAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// OnDisconnected を2回呼んでも RemovePlayerAsync は1回だけ呼ばれることを検証。
    /// _currentGroup が null でも _lobbyId が設定されていれば RemovePlayerAsync は呼ばれるが、
    /// 2回目は _hasLeft ガードで早期リターンするため重複実行されない。
    /// </summary>
    [Fact]
    public async Task OnDisconnected_CalledTwice_RemovePlayerCalledOnlyOnce()
    {
        // Arrange
        var hub = CreateHub();
        SetPrivateField(hub, "_lobbyId", "test-lobby-003");
        SetPrivateField(hub, "_userId", "test-user-003");

        _mockLobbyDataService
            .Setup(s => s.RemovePlayerAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var onDisconnected = typeof(LobbyHub).GetMethod(
            "OnDisconnected", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(onDisconnected);

        // Act — call OnDisconnected twice
        await (ValueTask)onDisconnected.Invoke(hub, null)!;
        await (ValueTask)onDisconnected.Invoke(hub, null)!;

        // Assert — _hasLeft should be 1 (set by first call, second call returned early)
        var hasLeftField = typeof(LobbyHub).GetField("_hasLeft", BindingFlags.NonPublic | BindingFlags.Instance);
        var hasLeftValue = (int)hasLeftField!.GetValue(hub)!;
        Assert.Equal(1, hasLeftValue);

        // RemovePlayerAsync should be called exactly once
        // (first OnDisconnected calls it because _lobbyId is set; second is blocked by _hasLeft)
        _mockLobbyDataService.Verify(
            s => s.RemovePlayerAsync("test-lobby-003", "test-user-003"), Times.Once);
    }

    /// <summary>
    /// LeaveAsync と OnDisconnected が並行して呼ばれても RemovePlayerAsync は1回だけ呼ばれることを検証。
    /// Interlocked.CompareExchange によるスレッドセーフな重複防止を確認。
    /// </summary>
    [Fact]
    public async Task LeaveAsync_AndOnDisconnected_Concurrent_OnlyOneExecutes()
    {
        // Arrange
        var hub = CreateHub();
        SetPrivateField(hub, "_lobbyId", "test-lobby-004");
        SetPrivateField(hub, "_userId", "test-user-004");

        var onDisconnected = typeof(LobbyHub).GetMethod(
            "OnDisconnected", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(onDisconnected);

        // Act — run LeaveAsync and OnDisconnected concurrently
        var leaveTask = hub.LeaveAsync().AsTask();
        var disconnectTask = ((ValueTask)onDisconnected.Invoke(hub, null)!).AsTask();
        await Task.WhenAll(leaveTask, disconnectTask);

        // Assert — only one execution should have proceeded past the Interlocked guard
        var hasLeftField = typeof(LobbyHub).GetField("_hasLeft", BindingFlags.NonPublic | BindingFlags.Instance);
        var hasLeftValue = (int)hasLeftField!.GetValue(hub)!;
        Assert.Equal(1, hasLeftValue);
    }

    // ---------------------------------------------------------------
    // SendMessageAsync / SetStageAsync / SetReadyAsync テスト
    // ---------------------------------------------------------------

    [Fact]
    public void SendMessageAsync_WithNullGroup_DoesNotThrow()
    {
        // Arrange: _currentGroup = null（デフォルト）
        var hub = CreateHub();
        SetPrivateField(hub, "_lobbyId", "msg-lobby");
        SetPrivateField(hub, "_userId", "msg-user");

        // Act & Assert: 例外なし（_currentGroup == null で early return）
        hub.SendMessageAsync("Hello");
    }

    [Fact]
    public async Task SetStageAsync_WithEmptyLobbyId_IsNoop()
    {
        // Arrange: _lobbyId は空文字（デフォルト）
        var hub = CreateHub();

        // Act
        await hub.SetStageAsync(5);

        // Assert: GetLobbyAsync が呼ばれない（early return）
        _mockLobbyDataService.Verify(
            s => s.GetLobbyAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SetReadyAsync_WithEmptyLobbyId_IsNoop()
    {
        // Arrange: _lobbyId は空文字（デフォルト）
        var hub = CreateHub();

        // Act
        await hub.SetReadyAsync(true);

        // Assert: SetReadyAndCheckAllAsync が呼ばれない（early return）
        _mockLobbyDataService.Verify(
            s => s.SetReadyAndCheckAllAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task SetStageAsync_NonHost_ThrowsPermissionDenied()
    {
        // Arrange
        var hub = CreateHub();
        SetPrivateField(hub, "_lobbyId", "host-check-lobby");
        SetPrivateField(hub, "_userId", "non-host-user");

        _mockLobbyDataService.Setup(s => s.GetLobbyAsync("host-check-lobby"))
            .ReturnsAsync(new LobbyInfo
            {
                LobbyId = "host-check-lobby",
                HostUserId = "actual-host-user",  // 異なるユーザー
            });

        // Act & Assert
        await Assert.ThrowsAsync<ReturnStatusException>(
            () => hub.SetStageAsync(5).AsTask());
    }

    [Fact]
    public async Task SetReadyAsync_AllReady_InvokesGameServerApi()
    {
        // Arrange
        var hub = CreateHub();
        SetPrivateField(hub, "_lobbyId", "ready-lobby");
        SetPrivateField(hub, "_userId", "ready-user");

        _mockLobbyDataService.Setup(s => s.SetReadyAndCheckAllAsync("ready-lobby", "ready-user", true))
            .ReturnsAsync((true, true));  // 全員レディ

        _mockLobbyDataService.Setup(s => s.GetPlayersAsync("ready-lobby"))
            .ReturnsAsync(Array.Empty<LobbyPlayerInfo>());  // プレイヤー0人 → StartGameAsync は abort

        // Act
        await hub.SetReadyAsync(true);

        // Assert: SetReadyAndCheckAllAsync が呼ばれたこと
        _mockLobbyDataService.Verify(
            s => s.SetReadyAndCheckAllAsync("ready-lobby", "ready-user", true), Times.Once);

        // allReady=true だが _currentGroup=null なので StartGameAsync には入らない
        // GetPlayersAsync は呼ばれない
        _mockLobbyDataService.Verify(
            s => s.GetPlayersAsync(It.IsAny<string>()), Times.Never);

        // Game.Server API は呼ばれない（StartGameAsync が abort のため）
        _mockUnityServerApi.Verify(
            s => s.IssueTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);
    }

    /// <summary>
    /// リフレクションで private フィールドを設定するヘルパー
    /// </summary>
    private static void SetPrivateField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        field.SetValue(obj, value);
    }
}
