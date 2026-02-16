using Game.Realtime.Hubs;
using Game.Realtime.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Game.Realtime.Tests.Hubs;

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

        // Act
        var hub = new ChatHub(logger.Object, chatMessageService.Object);

        // Assert
        Assert.NotNull(hub);
    }

    [Fact]
    public void ChatHub_ImplementsIChatHub()
    {
        // Arrange
        var logger = new Mock<ILogger<ChatHub>>();
        var chatMessageService = new Mock<IChatMessageService>();

        // Act
        var hub = new ChatHub(logger.Object, chatMessageService.Object);

        // Assert
        Assert.IsAssignableFrom<Game.Library.Shared.Realtime.Hubs.IChatHub>(hub);
    }
}
