using System.Security.Claims;
using Game.Library.Shared.Dto;
using Game.Library.Shared.Enums;
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

    private (ChatHub Hub, Mock<IGroupManager> Groups, Mock<IChatHubClient> Client, Dictionary<object, object?> Items) CreateHubWithContext(
        string connectionId = "test-conn",
        string userId = "user-1")
    {
        var hub = CreateHub();

        var items = new Dictionary<object, object?>();
        var mockContext = new Mock<HubCallerContext>();
        mockContext.Setup(c => c.ConnectionId).Returns(connectionId);
        mockContext.Setup(c => c.Items).Returns(items);

        var claims = new[] { new Claim("sub", userId) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        mockContext.Setup(c => c.User).Returns(new ClaimsPrincipal(identity));

        var mockGroups = new Mock<IGroupManager>();
        var mockClient = new Mock<IChatHubClient>();
        var mockClients = new Mock<IHubCallerClients<IChatHubClient>>();
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClient.Object);

        hub.Context = mockContext.Object;
        hub.Groups = mockGroups.Object;
        hub.Clients = mockClients.Object;

        return (hub, mockGroups, mockClient, items);
    }

    private void SetupRoomMocks(string roomId, int permissions = (int)(ChatRoomPermissions.Join | ChatRoomPermissions.Leave | ChatRoomPermissions.SendMessage))
    {
        _roomDataService.Setup(r => r.ExistsAsync(roomId)).ReturnsAsync(true);
        _roomDataService.Setup(r => r.GetDefaultPermissionsAsync(roomId)).ReturnsAsync(permissions);
        _roomDataService.Setup(r => r.AddMemberAsync(roomId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(true);
        _roomDataService.Setup(r => r.GetMemberPermissionsAsync(roomId, It.IsAny<string>())).ReturnsAsync(permissions);
        _roomDataService.Setup(r => r.GetMembersAsync(roomId)).ReturnsAsync(new[]
        {
            new ChatRoomMemberInfo { UserId = "user-1", PlayerName = "Player1", Permissions = permissions },
        });
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

    [Fact]
    public async Task JoinAsync_AddsRoomToConnectionItems()
    {
        SetupRoomMocks("room1");
        var (hub, mockGroups, mockClient, items) = CreateHubWithContext();

        await hub.JoinAsync("room1", "Player1");

        mockGroups.Verify(g => g.AddToGroupAsync("test-conn", "room1", default), Times.Once);
        mockClient.Verify(c => c.OnPlayerJoined("room1", "user-1", "Player1"), Times.Once);
        Assert.True(items.ContainsKey("JoinedRooms"));
        var rooms = (HashSet<string>)items["JoinedRooms"]!;
        Assert.Contains("room1", rooms);
    }

    [Fact]
    public async Task LeaveAsync_RemovesRoomFromConnectionItems()
    {
        SetupRoomMocks("room1");
        var (hub, mockGroups, mockClient, items) = CreateHubWithContext();

        await hub.JoinAsync("room1", "Player1");
        await hub.LeaveAsync("room1");

        mockGroups.Verify(g => g.RemoveFromGroupAsync("test-conn", "room1", default), Times.Once);
        mockClient.Verify(c => c.OnPlayerLeft("room1", "user-1", "Player1"), Times.Once);
        var rooms = (HashSet<string>)items["JoinedRooms"]!;
        Assert.Empty(rooms);
    }

    [Fact]
    public async Task OnDisconnectedAsync_CleansUpAllRooms()
    {
        SetupRoomMocks("room1");
        SetupRoomMocks("room2");
        var (hub, mockGroups, mockClient, _) = CreateHubWithContext();

        await hub.JoinAsync("room1", "Player1");
        await hub.JoinAsync("room2", "Player1");
        await hub.OnDisconnectedAsync(null);

        mockGroups.Verify(g => g.RemoveFromGroupAsync("test-conn", "room1", default), Times.Once);
        mockGroups.Verify(g => g.RemoveFromGroupAsync("test-conn", "room2", default), Times.Once);
        _roomDataService.Verify(r => r.RemoveMemberAsync("room1", "user-1"), Times.Once);
        _roomDataService.Verify(r => r.RemoveMemberAsync("room2", "user-1"), Times.Once);
        mockClient.Verify(c => c.OnPlayerLeft("room1", "user-1", "Player1"), Times.Once);
        mockClient.Verify(c => c.OnPlayerLeft("room2", "user-1", "Player1"), Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_NoErrorWhenNoRoomsJoined()
    {
        var (hub, mockGroups, _, _) = CreateHubWithContext();

        await hub.OnDisconnectedAsync(null);

        mockGroups.Verify(g => g.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }
}
