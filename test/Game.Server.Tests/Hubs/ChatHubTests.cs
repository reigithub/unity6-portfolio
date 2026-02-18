using Game.Library.Shared.Dto;
using Game.Server.Hubs;
using Game.Server.Services.Chat;
using Microsoft.Extensions.Logging;
using Moq;

namespace Game.Server.Tests.Hubs;

/// <summary>
/// ChatHub のテスト
/// </summary>
public class ChatHubTests
{
    [Fact]
    public void ChatHub_CanBeInstantiated()
    {
        // Arrange
        var logger = new Mock<ILogger<ChatHub>>();
        var chatMessageService = new Mock<IChatMessageService>();
        var roomDataService = new Mock<IChatRoomDataService>();
        var validator = new ChatPermissionValidator(roomDataService.Object);

        // Act
        var hub = new ChatHub(logger.Object, chatMessageService.Object, roomDataService.Object, validator);

        // Assert
        Assert.NotNull(hub);
    }

    [Fact]
    public void ChatHub_ImplementsHubOfIChatHubClient()
    {
        // Arrange
        var logger = new Mock<ILogger<ChatHub>>();
        var chatMessageService = new Mock<IChatMessageService>();
        var roomDataService = new Mock<IChatRoomDataService>();
        var validator = new ChatPermissionValidator(roomDataService.Object);

        // Act
        var hub = new ChatHub(logger.Object, chatMessageService.Object, roomDataService.Object, validator);

        // Assert
        Assert.IsAssignableFrom<Microsoft.AspNetCore.SignalR.Hub<IChatHubClient>>(hub);
    }

    [Fact]
    public async Task GetRecentMessagesAsync_ReturnsEmptyArray_WhenRoomIdEmpty()
    {
        // Arrange
        var logger = new Mock<ILogger<ChatHub>>();
        var chatMessageService = new Mock<IChatMessageService>();
        var roomDataService = new Mock<IChatRoomDataService>();
        var validator = new ChatPermissionValidator(roomDataService.Object);
        var hub = new ChatHub(logger.Object, chatMessageService.Object, roomDataService.Object, validator);

        // Act
        var result = await hub.GetRecentMessagesAsync("", 10);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetRecentMessagesAsync_DelegatesToService()
    {
        // Arrange
        var logger = new Mock<ILogger<ChatHub>>();
        var chatMessageService = new Mock<IChatMessageService>();
        var roomDataService = new Mock<IChatRoomDataService>();
        var validator = new ChatPermissionValidator(roomDataService.Object);

        var expectedMessages = new[]
        {
            new ChatMessage { UserId = "user1", Content = "Hello", Timestamp = 1000 },
        };
        chatMessageService.Setup(x => x.GetRecentMessagesAsync("room1", 10))
            .ReturnsAsync(expectedMessages);

        var hub = new ChatHub(logger.Object, chatMessageService.Object, roomDataService.Object, validator);

        // Act
        var result = await hub.GetRecentMessagesAsync("room1", 10);

        // Assert
        Assert.Single(result);
        Assert.Equal("user1", result[0].UserId);
    }
}
