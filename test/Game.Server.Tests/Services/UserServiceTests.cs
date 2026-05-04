using Game.Library.Shared.Dto;
using Game.Server.Dto.Responses;
using Game.Server.Repositories.Interfaces;
using Game.Server.Services;
using Game.Server.Tables;
using Moq;
using Npgsql;

namespace Game.Server.Tests.Services;

public class UserServiceTests
{
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly UserService _service;

    public UserServiceTests()
    {
        _mockUserRepo = new Mock<IUserRepository>();
        _service = new UserService(_mockUserRepo.Object);
    }

    [Fact]
    public async Task UpdateUserAsync_ReturnsConflict_WhenUniqueViolation()
    {
        // Arrange
        var userId = "test-user-001";
        var existingUser = new UserInfo
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserName = "OldName",
            Level = 1,
            RegisteredAt = DateTime.UtcNow,
            AuthType = "Guest",
        };

        _mockUserRepo.Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(existingUser);
        _mockUserRepo.Setup(r => r.GetByUserNameAsync("TakenName"))
            .ReturnsAsync((UserInfo?)null); // CHECK passes (race window)
        _mockUserRepo.Setup(r => r.UpdateAsync(It.IsAny<UserInfo>()))
            .ThrowsAsync(CreateUniqueViolationException()); // ACT fails

        var request = new UpdateUserRequest { UserName = "TakenName" };

        // Act
        var result = await _service.UpdateUserAsync(userId, request);

        // Assert
        Assert.True(result.IsError);
        var error = AuthServiceTests.ExtractError(result);
        Assert.NotNull(error);
        Assert.Equal("DUPLICATE_NAME", error.ErrorCode);
        Assert.Equal(409, error.StatusCode);
    }

    [Fact]
    public async Task UpdateUserAsync_ReturnsSuccess_WhenNoConflict()
    {
        // Arrange
        var userId = "test-user-002";
        var existingUser = new UserInfo
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserName = "OldName",
            Level = 1,
            RegisteredAt = DateTime.UtcNow,
            AuthType = "Guest",
        };

        _mockUserRepo.Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(existingUser);
        _mockUserRepo.Setup(r => r.GetByUserNameAsync("NewName"))
            .ReturnsAsync((UserInfo?)null);
        _mockUserRepo.Setup(r => r.UpdateAsync(It.IsAny<UserInfo>()))
            .Returns(Task.CompletedTask);

        var request = new UpdateUserRequest { UserName = "NewName" };

        // Act
        var result = await _service.UpdateUserAsync(userId, request);

        // Assert
        Assert.False(result.IsError);
        var response = AuthServiceTests.ExtractSuccess(result);
        Assert.NotNull(response);
        Assert.Equal("NewName", response.UserName);
    }

    /// <summary>
    /// PostgresException with SqlState "23505" (unique_violation) を生成する。
    /// Npgsql 9.x は public コンストラクタを持つ。
    /// </summary>
    private static PostgresException CreateUniqueViolationException()
    {
        return new PostgresException(
            messageText: "duplicate key value violates unique constraint",
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: "23505");
    }
}
