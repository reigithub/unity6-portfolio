using Game.Library.Shared.Dto;
using Game.Library.Shared.Enums;
using Game.Server.Services.Chat;
using Game.Server.Services.Chat.Exceptions;
using Moq;

namespace Game.Server.Tests.Services;

/// <summary>
/// ChatPermissionValidator のテスト
/// </summary>
public class ChatPermissionValidatorTests
{
    private readonly Mock<IChatRoomDataService> _roomDataServiceMock;
    private readonly ChatPermissionValidator _validator;

    public ChatPermissionValidatorTests()
    {
        _roomDataServiceMock = new Mock<IChatRoomDataService>();
        _validator = new ChatPermissionValidator(_roomDataServiceMock.Object);
    }

    [Fact]
    public async Task ValidateAsync_DoesNotThrow_WhenHasPermission()
    {
        // Arrange
        var permissions = (int)(ChatRoomPermissions.SendMessage | ChatRoomPermissions.Leave);
        _roomDataServiceMock.Setup(x => x.GetMemberPermissionsAsync("room1", "user1"))
            .ReturnsAsync(permissions);

        // Act & Assert: 例外なしで完了
        await _validator.ValidateAsync("room1", "user1", ChatRoomPermissions.SendMessage);
    }

    [Fact]
    public async Task ValidateAsync_Throws_WhenMissingPermission()
    {
        // Arrange
        var permissions = (int)ChatRoomPermissions.SendMessage;
        _roomDataServiceMock.Setup(x => x.GetMemberPermissionsAsync("room1", "user1"))
            .ReturnsAsync(permissions);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ChatPermissionException>(
            () => _validator.ValidateAsync("room1", "user1", ChatRoomPermissions.Delete));
        Assert.Contains("Missing permission", ex.Message);
    }

    [Fact]
    public async Task ValidateAsync_Throws_WhenNotMember()
    {
        // Arrange
        _roomDataServiceMock.Setup(x => x.GetMemberPermissionsAsync("room1", "nonmember"))
            .ReturnsAsync(0);

        // Act & Assert
        await Assert.ThrowsAsync<ChatPermissionException>(
            () => _validator.ValidateAsync("room1", "nonmember", ChatRoomPermissions.SendMessage));
    }

    [Fact]
    public async Task HasPermissionAsync_ReturnsTrue_WhenHasPermission()
    {
        // Arrange
        var permissions = (int)(ChatRoomPermissions.Join | ChatRoomPermissions.SendMessage | ChatRoomPermissions.Leave);
        _roomDataServiceMock.Setup(x => x.GetMemberPermissionsAsync("room1", "user1"))
            .ReturnsAsync(permissions);

        // Act
        var result = await _validator.HasPermissionAsync("room1", "user1", ChatRoomPermissions.SendMessage);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task HasPermissionAsync_ReturnsFalse_WhenMissingPermission()
    {
        // Arrange
        var permissions = (int)ChatRoomPermissions.SendMessage;
        _roomDataServiceMock.Setup(x => x.GetMemberPermissionsAsync("room1", "user1"))
            .ReturnsAsync(permissions);

        // Act
        var result = await _validator.HasPermissionAsync("room1", "user1", ChatRoomPermissions.Delete);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateRoomExistsAsync_DoesNotThrow_WhenRoomExists()
    {
        // Arrange
        _roomDataServiceMock.Setup(x => x.ExistsAsync("room1"))
            .ReturnsAsync(true);

        // Act & Assert
        await _validator.ValidateRoomExistsAsync("room1");
    }

    [Fact]
    public async Task ValidateRoomExistsAsync_Throws_WhenRoomNotExists()
    {
        // Arrange
        _roomDataServiceMock.Setup(x => x.ExistsAsync("nonexistent"))
            .ReturnsAsync(false);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ChatNotFoundException>(
            () => _validator.ValidateRoomExistsAsync("nonexistent"));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task HasDefaultPermissionAsync_ReturnsTrue_WhenDefaultPermissionIncludes()
    {
        // Arrange
        _roomDataServiceMock.Setup(x => x.GetDefaultPermissionsAsync("room1"))
            .ReturnsAsync(7);

        // Act
        var result = await _validator.HasDefaultPermissionAsync("room1", ChatRoomPermissions.Join);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task HasDefaultPermissionAsync_ReturnsFalse_WhenDefaultPermissionExcludes()
    {
        // Arrange
        _roomDataServiceMock.Setup(x => x.GetDefaultPermissionsAsync("room1"))
            .ReturnsAsync(6);

        // Act
        var result = await _validator.HasDefaultPermissionAsync("room1", ChatRoomPermissions.Join);

        // Assert
        Assert.False(result);
    }
}
