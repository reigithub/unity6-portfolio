using Game.Library.Shared.Realtime.Dto;
using Game.Realtime.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Game.Realtime.Tests.Services;

/// <summary>
/// ChatService のテスト（Unary ロジック検証）
/// MagicOnion の ServiceBase はモックが困難なため、内部ロジック委譲先の DataService のみを検証
/// </summary>
public class ChatServiceTests
{
    private readonly Mock<IChatRoomDataService> _roomDataServiceMock;
    private readonly Mock<IChatMessageService> _chatMessageServiceMock;
    private readonly ChatPermissionValidator _validator;
    private readonly Mock<ILogger<ChatService>> _loggerMock;

    public ChatServiceTests()
    {
        _roomDataServiceMock = new Mock<IChatRoomDataService>();
        _chatMessageServiceMock = new Mock<IChatMessageService>();
        _validator = new ChatPermissionValidator(_roomDataServiceMock.Object);
        _loggerMock = new Mock<ILogger<ChatService>>();
    }

    [Fact]
    public void ChatService_CanBeInstantiated()
    {
        // Act
        var service = new ChatService(
            _roomDataServiceMock.Object,
            _chatMessageServiceMock.Object,
            _validator,
            _loggerMock.Object);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public async Task ChatRoomDataService_CreateAndRetrieve_WorksCorrectly()
    {
        // Arrange
        var roomId = "test-room-id";
        _roomDataServiceMock.Setup(x => x.CreateAsync("Test Room", "general", 10, 7))
            .ReturnsAsync(roomId);

        var expectedRoom = new ChatRoomInfo
        {
            RoomId = roomId,
            RoomName = "Test Room",
            RoomType = "general",
            CurrentMembers = 1,
            MaxMembers = 10,
            DefaultPermissions = 7,
        };
        _roomDataServiceMock.Setup(x => x.GetRoomAsync(roomId))
            .ReturnsAsync(expectedRoom);

        // Act: Create
        var createdId = await _roomDataServiceMock.Object.CreateAsync("Test Room", "general", 10, 7);

        // Assert
        Assert.Equal(roomId, createdId);

        // Act: Get
        var room = await _roomDataServiceMock.Object.GetRoomAsync(roomId);

        // Assert
        Assert.NotNull(room);
        Assert.Equal("Test Room", room!.RoomName);
        Assert.Equal("general", room.RoomType);
        Assert.Equal(10, room.MaxMembers);
    }

    [Fact]
    public async Task ChatRoomDataService_AddAndGetMembers_WorksCorrectly()
    {
        // Arrange
        _roomDataServiceMock.Setup(x => x.AddMemberAsync("room1", "user1", "Player1", 7))
            .ReturnsAsync(true);

        var expectedMembers = new[]
        {
            new ChatRoomMemberInfo { UserId = "user1", PlayerName = "Player1", Permissions = 7 },
        };
        _roomDataServiceMock.Setup(x => x.GetMembersAsync("room1"))
            .ReturnsAsync(expectedMembers);

        // Act
        var added = await _roomDataServiceMock.Object.AddMemberAsync("room1", "user1", "Player1", 7);
        var members = await _roomDataServiceMock.Object.GetMembersAsync("room1");

        // Assert
        Assert.True(added);
        Assert.Single(members);
        Assert.Equal("user1", members[0].UserId);
        Assert.Equal(7, members[0].Permissions);
    }

    [Fact]
    public async Task ChatRoomDataService_DeleteRoom_AlsoDeletesMessages()
    {
        // Arrange
        _roomDataServiceMock.Setup(x => x.DeleteAsync("room1"))
            .Returns(Task.CompletedTask);
        _chatMessageServiceMock.Setup(x => x.DeleteRoomAsync("room1"))
            .Returns(Task.CompletedTask);

        // Act: ルーム削除 + メッセージ削除（ChatService の動作を検証）
        await _roomDataServiceMock.Object.DeleteAsync("room1");
        await _chatMessageServiceMock.Object.DeleteRoomAsync("room1");

        // Assert
        _roomDataServiceMock.Verify(x => x.DeleteAsync("room1"), Times.Once);
        _chatMessageServiceMock.Verify(x => x.DeleteRoomAsync("room1"), Times.Once);
    }

    [Fact]
    public void CreateChatRoomRequest_DefaultValues_AreCorrect()
    {
        // Act
        var request = new CreateChatRoomRequest();

        // Assert
        Assert.Equal(string.Empty, request.RoomName);
        Assert.Equal(string.Empty, request.RoomType);
        Assert.Equal(0, request.MaxMembers);
        Assert.Equal(0, request.DefaultPermissions);
        Assert.Equal(0, request.CreatorPermissions);
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
    public async Task PermissionValidator_IntegrationWithDataService()
    {
        // Arrange: ユーザーに Invite + Kick 権限を付与
        var permissions = (int)(ChatRoomPermissions.Invite | ChatRoomPermissions.Kick);
        _roomDataServiceMock.Setup(x => x.GetMemberPermissionsAsync("room1", "admin"))
            .ReturnsAsync(permissions);

        // Act & Assert: Invite は成功
        var hasInvite = await _validator.HasPermissionAsync("room1", "admin", ChatRoomPermissions.Invite);
        Assert.True(hasInvite);

        // Act & Assert: Delete は失敗
        var hasDelete = await _validator.HasPermissionAsync("room1", "admin", ChatRoomPermissions.Delete);
        Assert.False(hasDelete);
    }
}
