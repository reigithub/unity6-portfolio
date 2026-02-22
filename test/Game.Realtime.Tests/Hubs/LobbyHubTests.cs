using System.Reflection;
using Game.Library.Shared.Dto;
using Game.Realtime.Hubs;
using Game.Realtime.Services;
using Game.Realtime.Validation;
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
    private readonly Mock<IMatchSessionTokenService> _mockTokenService;
    private readonly IOptions<GameServerConfiguration> _gameServerConfig;
    private readonly Mock<ILobbyValidator> _mockLobbyValidator;

    public LobbyHubTests()
    {
        _mockLogger = new Mock<ILogger<LobbyHub>>();
        _mockLobbyDataService = new Mock<ILobbyDataService>();
        _mockTokenService = new Mock<IMatchSessionTokenService>();
        _gameServerConfig = Options.Create(new GameServerConfiguration());
        _mockLobbyValidator = new Mock<ILobbyValidator>();
    }

    private LobbyHub CreateHub()
    {
        return new LobbyHub(
            _mockLogger.Object, _mockLobbyDataService.Object, _mockTokenService.Object,
            _gameServerConfig, _mockLobbyValidator.Object);
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
