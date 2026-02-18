using System.Security.Claims;
using Game.Library.Shared.Dto;
using Game.Library.Shared.Enums;
using Game.Server.Controllers;
using Game.Server.Hubs;
using Game.Server.Services.Chat;
using Game.Server.Services.Chat.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace Game.Server.Tests.Controllers;

/// <summary>
/// ChatRoomController のテスト
/// </summary>
public class ChatRoomControllerTests
{
    private readonly Mock<IChatRoomDataService> _roomDataServiceMock;
    private readonly Mock<IChatMessageService> _chatMessageServiceMock;
    private readonly ChatPermissionValidator _validator;
    private readonly Mock<IHubContext<ChatHub, IChatHubClient>> _hubContextMock;
    private readonly Mock<ILogger<ChatRoomController>> _loggerMock;
    private readonly ChatRoomController _controller;

    public ChatRoomControllerTests()
    {
        _roomDataServiceMock = new Mock<IChatRoomDataService>();
        _chatMessageServiceMock = new Mock<IChatMessageService>();
        _validator = new ChatPermissionValidator(_roomDataServiceMock.Object);
        _hubContextMock = new Mock<IHubContext<ChatHub, IChatHubClient>>();
        _loggerMock = new Mock<ILogger<ChatRoomController>>();

        _controller = new ChatRoomController(
            _roomDataServiceMock.Object,
            _chatMessageServiceMock.Object,
            _validator,
            _hubContextMock.Object,
            _loggerMock.Object);

        // 認証済みユーザーを設定
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "test-user-id") };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal },
        };
    }

    [Fact]
    public async Task CreateRoom_ReturnsOk_WithRoomId()
    {
        // Arrange
        _roomDataServiceMock.Setup(x => x.CreateAsync("Test Room", "general", 10, 7))
            .ReturnsAsync("new-room-id");
        _roomDataServiceMock.Setup(x => x.AddMemberAsync("new-room-id", "test-user-id", "Test Room", 255))
            .ReturnsAsync(true);

        var request = new CreateChatRoomRequest
        {
            RoomName = "Test Room",
            RoomType = "general",
            MaxMembers = 10,
            DefaultPermissions = 7,
            CreatorPermissions = 255,
        };

        // Act
        var result = await _controller.CreateRoom(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CreateChatRoomResponse>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal("new-room-id", response.RoomId);
    }

    [Fact]
    public async Task GetRoomInfo_ReturnsNotFound_WhenRoomNotExists()
    {
        // Arrange
        _roomDataServiceMock.Setup(x => x.GetRoomAsync("nonexistent"))
            .ReturnsAsync((ChatRoomInfo?)null);

        // Act
        var result = await _controller.GetRoomInfo("nonexistent");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetRoomInfo_ReturnsOk_WhenRoomExists()
    {
        // Arrange
        var roomInfo = new ChatRoomInfo
        {
            RoomId = "room1",
            RoomName = "Test Room",
            RoomType = "general",
            CurrentMembers = 2,
            MaxMembers = 10,
        };
        _roomDataServiceMock.Setup(x => x.GetRoomAsync("room1"))
            .ReturnsAsync(roomInfo);

        // Act
        var result = await _controller.GetRoomInfo("room1");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ChatRoomInfo>(okResult.Value);
        Assert.Equal("room1", response.RoomId);
        Assert.Equal("Test Room", response.RoomName);
    }

    [Fact]
    public async Task DeleteRoom_Returns403_WhenNoPermission()
    {
        // Arrange
        _roomDataServiceMock.Setup(x => x.ExistsAsync("room1"))
            .ReturnsAsync(true);
        _roomDataServiceMock.Setup(x => x.GetMemberPermissionsAsync("room1", "test-user-id"))
            .ReturnsAsync(0); // 権限なし

        // Act
        var result = await _controller.DeleteRoom("room1");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);
    }

    [Fact]
    public async Task DeleteRoom_Returns404_WhenRoomNotExists()
    {
        // Arrange
        _roomDataServiceMock.Setup(x => x.ExistsAsync("nonexistent"))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteRoom("nonexistent");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetMembers_ReturnsOk_WithMemberList()
    {
        // Arrange
        _roomDataServiceMock.Setup(x => x.ExistsAsync("room1"))
            .ReturnsAsync(true);
        var members = new[]
        {
            new ChatRoomMemberInfo { UserId = "user1", PlayerName = "Player1", Permissions = 7 },
            new ChatRoomMemberInfo { UserId = "user2", PlayerName = "Player2", Permissions = 7 },
        };
        _roomDataServiceMock.Setup(x => x.GetMembersAsync("room1"))
            .ReturnsAsync(members);

        // Act
        var result = await _controller.GetMembers("room1");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ChatRoomMembersResponse>(okResult.Value);
        Assert.Equal(2, response.Members.Length);
    }

    [Fact]
    public async Task InviteMember_Returns403_WhenNoPermission()
    {
        // Arrange
        _roomDataServiceMock.Setup(x => x.ExistsAsync("room1"))
            .ReturnsAsync(true);
        _roomDataServiceMock.Setup(x => x.GetMemberPermissionsAsync("room1", "test-user-id"))
            .ReturnsAsync((int)ChatRoomPermissions.SendMessage); // Invite 権限なし

        var request = new InviteMemberRequest
        {
            TargetUserId = "user2",
            PlayerName = "Player2",
        };

        // Act
        var result = await _controller.InviteMember("room1", request);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);
    }

    [Fact]
    public void ChatRoomPermissions_BitwiseCombination_WorksCorrectly()
    {
        // Arrange
        var permissions = ChatRoomPermissions.Join | ChatRoomPermissions.SendMessage | ChatRoomPermissions.Leave;

        // Assert
        Assert.True(permissions.HasFlag(ChatRoomPermissions.Join));
        Assert.True(permissions.HasFlag(ChatRoomPermissions.SendMessage));
        Assert.True(permissions.HasFlag(ChatRoomPermissions.Leave));
        Assert.False(permissions.HasFlag(ChatRoomPermissions.Delete));
        Assert.False(permissions.HasFlag(ChatRoomPermissions.ManageRoom));
    }

    [Fact]
    public void CreateChatRoomResponse_DefaultValues_AreCorrect()
    {
        // Act
        var response = new CreateChatRoomResponse();

        // Assert
        Assert.False(response.Success);
        Assert.Equal(string.Empty, response.RoomId);
        Assert.Equal(string.Empty, response.ErrorMessage);
    }

    [Fact]
    public void ChatRoomInfo_DefaultValues_AreCorrect()
    {
        // Act
        var info = new ChatRoomInfo();

        // Assert
        Assert.Equal(string.Empty, info.RoomId);
        Assert.Equal(string.Empty, info.RoomName);
        Assert.Equal(string.Empty, info.RoomType);
        Assert.Equal(0, info.CurrentMembers);
        Assert.Equal(0, info.MaxMembers);
        Assert.Equal(0, info.CreatedAt);
        Assert.Equal(0, info.DefaultPermissions);
    }

    [Fact]
    public void ChatRoomMemberInfo_DefaultValues_AreCorrect()
    {
        // Act
        var info = new ChatRoomMemberInfo();

        // Assert
        Assert.Equal(string.Empty, info.UserId);
        Assert.Equal(string.Empty, info.PlayerName);
        Assert.Equal(0, info.JoinedAt);
        Assert.Equal(0, info.Permissions);
    }
}
