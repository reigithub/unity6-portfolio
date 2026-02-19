using Game.Server.Shared.Exceptions;
using Game.Server.Validation;

namespace Game.Server.Tests.Validation;

public class ChatInputValidatorTests
{
    private readonly ChatInputValidator _validator = new();

    // --- ValidateRoomId ---

    [Theory]
    [InlineData("room-123")]
    [InlineData("a")]
    public void ValidateRoomId_ValidInput_DoesNotThrow(string roomId)
    {
        _validator.ValidateRoomId(roomId);
    }

    [Fact]
    public void ValidateRoomId_ExactlyMaxLength_DoesNotThrow()
    {
        _validator.ValidateRoomId(new string('a', 64));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateRoomId_NullOrWhitespace_ThrowsErrorException(string? roomId)
    {
        var ex = Assert.Throws<ErrorException>(() => _validator.ValidateRoomId(roomId!));
        Assert.Equal("INVALID_INPUT", ex.ErrorCode);
    }

    [Fact]
    public void ValidateRoomId_ExceedsMaxLength_ThrowsErrorException()
    {
        var ex = Assert.Throws<ErrorException>(() => _validator.ValidateRoomId(new string('a', 65)));
        Assert.Equal("INVALID_INPUT", ex.ErrorCode);
    }

    // --- ValidatePlayerName ---

    [Theory]
    [InlineData("Player1")]
    [InlineData("a")]
    public void ValidatePlayerName_ValidInput_DoesNotThrow(string name)
    {
        _validator.ValidatePlayerName(name);
    }

    [Fact]
    public void ValidatePlayerName_ExactlyMaxLength_DoesNotThrow()
    {
        _validator.ValidatePlayerName(new string('a', 50));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidatePlayerName_NullOrWhitespace_ThrowsErrorException(string? name)
    {
        var ex = Assert.Throws<ErrorException>(() => _validator.ValidatePlayerName(name!));
        Assert.Equal("INVALID_INPUT", ex.ErrorCode);
    }

    [Fact]
    public void ValidatePlayerName_ExceedsMaxLength_ThrowsErrorException()
    {
        var ex = Assert.Throws<ErrorException>(() => _validator.ValidatePlayerName(new string('a', 51)));
        Assert.Equal("INVALID_INPUT", ex.ErrorCode);
    }

    // --- ValidateMessageContent ---

    [Theory]
    [InlineData("Hello")]
    [InlineData("a")]
    public void ValidateMessageContent_ValidInput_DoesNotThrow(string content)
    {
        _validator.ValidateMessageContent(content);
    }

    [Fact]
    public void ValidateMessageContent_ExactlyMaxLength_DoesNotThrow()
    {
        _validator.ValidateMessageContent(new string('a', 500));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateMessageContent_NullOrWhitespace_ThrowsErrorException(string? content)
    {
        var ex = Assert.Throws<ErrorException>(() => _validator.ValidateMessageContent(content!));
        Assert.Equal("INVALID_INPUT", ex.ErrorCode);
    }

    [Fact]
    public void ValidateMessageContent_ExceedsMaxLength_ThrowsErrorException()
    {
        var ex = Assert.Throws<ErrorException>(() => _validator.ValidateMessageContent(new string('a', 501)));
        Assert.Equal("INVALID_INPUT", ex.ErrorCode);
    }

    // --- ValidateMessageCount ---

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void ValidateMessageCount_ValidInput_DoesNotThrow(int count)
    {
        _validator.ValidateMessageCount(count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void ValidateMessageCount_ZeroOrNegative_ThrowsErrorException(int count)
    {
        var ex = Assert.Throws<ErrorException>(() => _validator.ValidateMessageCount(count));
        Assert.Equal("INVALID_INPUT", ex.ErrorCode);
    }

    [Fact]
    public void ValidateMessageCount_ExceedsMax_ThrowsErrorException()
    {
        var ex = Assert.Throws<ErrorException>(() => _validator.ValidateMessageCount(101));
        Assert.Equal("INVALID_INPUT", ex.ErrorCode);
    }
}
