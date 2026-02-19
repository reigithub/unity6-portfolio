using Game.Library.Shared.Dto;
using Game.Server.Hubs;
using Game.Server.Services.Chat;
using Game.Server.Shared.Exceptions;
using Game.Server.Validation;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace Game.Server.Tests.Hubs;

/// <summary>
/// ChatHub のテスト
/// </summary>
public class ChatHubTests
{
    private readonly Mock<ILogger<ChatHub>> _logger;
    private readonly Mock<IChatMessageService> _chatMessageService;
    private readonly Mock<IChatRoomDataService> _roomDataService;
    private readonly ChatPermissionValidator _validator;
    private readonly Mock<IChatInputValidator> _chatInputValidator;

    public ChatHubTests()
    {
        _logger = new Mock<ILogger<ChatHub>>();
        _chatMessageService = new Mock<IChatMessageService>();
        _roomDataService = new Mock<IChatRoomDataService>();
        _validator = new ChatPermissionValidator(_roomDataService.Object);
        _chatInputValidator = new Mock<IChatInputValidator>();
    }

    private ChatHub CreateHub()
    {
        return new ChatHub(
            _logger.Object,
            _chatMessageService.Object,
            _roomDataService.Object,
            _validator,
            _chatInputValidator.Object);
    }

    [Fact]
    public void ChatHub_CanBeInstantiated()
    {
        var hub = CreateHub();
        Assert.NotNull(hub);
    }

    [Fact]
    public void ChatHub_ImplementsHubOfIChatHubClient()
    {
        var hub = CreateHub();
        Assert.IsAssignableFrom<Hub<IChatHubClient>>(hub);
    }

    [Fact]
    public async Task GetRecentMessagesAsync_ThrowsErrorException_WhenRoomIdEmpty()
    {
        _chatInputValidator
            .Setup(v => v.ValidateRoomId(""))
            .Throws(new ErrorException("INVALID_INPUT", "Room ID is required and must not exceed 64 characters."));

        var hub = CreateHub();

        await Assert.ThrowsAsync<ErrorException>(() => hub.GetRecentMessagesAsync("", 10));
    }

    [Fact]
    public async Task GetRecentMessagesAsync_DelegatesToService()
    {
        var expectedMessages = new[]
        {
            new ChatMessage { UserId = "user1", Content = "Hello", Timestamp = 1000 },
        };
        _chatMessageService.Setup(x => x.GetRecentMessagesAsync("room1", 10))
            .ReturnsAsync(expectedMessages);

        var hub = CreateHub();

        var result = await hub.GetRecentMessagesAsync("room1", 10);

        Assert.Single(result);
        Assert.Equal("user1", result[0].UserId);
    }

    [Fact]
    public async Task GetRecentMessagesAsync_ThrowsErrorException_WhenRoomIdTooLong()
    {
        var longRoomId = new string('x', 65);
        _chatInputValidator
            .Setup(v => v.ValidateRoomId(longRoomId))
            .Throws(new ErrorException("INVALID_INPUT", "Room ID is required and must not exceed 64 characters."));

        var hub = CreateHub();

        await Assert.ThrowsAsync<ErrorException>(() => hub.GetRecentMessagesAsync(longRoomId, 10));
    }

    [Fact]
    public async Task GetRecentMessagesAsync_ThrowsErrorException_WhenCountOutOfRange()
    {
        _chatInputValidator
            .Setup(v => v.ValidateMessageCount(200))
            .Throws(new ErrorException("INVALID_INPUT", "Message count must be between 1 and 100."));

        var hub = CreateHub();

        await Assert.ThrowsAsync<ErrorException>(() => hub.GetRecentMessagesAsync("room1", 200));
    }

    [Fact]
    public async Task GetRecentMessagesAsync_ThrowsErrorException_WhenCountZeroOrNegative()
    {
        _chatInputValidator
            .Setup(v => v.ValidateMessageCount(0))
            .Throws(new ErrorException("INVALID_INPUT", "Message count must be between 1 and 100."));

        var hub = CreateHub();

        await Assert.ThrowsAsync<ErrorException>(() => hub.GetRecentMessagesAsync("room1", 0));
    }
}
