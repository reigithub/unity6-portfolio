using Game.Server.Validation;

namespace Game.Server.Tests.Validation;

public class PasswordValidatorTests
{
    [Fact]
    public void Validate_ValidPassword_ReturnsTrue()
    {
        var (isValid, errorMessage) = PasswordValidator.Validate("Password1!");
        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void Validate_TooShort_ReturnsFalse()
    {
        var (isValid, errorMessage) = PasswordValidator.Validate("Pa1!");
        Assert.False(isValid);
        Assert.Contains("8 characters", errorMessage);
    }

    [Fact]
    public void Validate_NoUppercase_ReturnsFalse()
    {
        var (isValid, errorMessage) = PasswordValidator.Validate("password1!");
        Assert.False(isValid);
        Assert.Contains("uppercase", errorMessage);
    }

    [Fact]
    public void Validate_NoLowercase_ReturnsFalse()
    {
        var (isValid, errorMessage) = PasswordValidator.Validate("PASSWORD1!");
        Assert.False(isValid);
        Assert.Contains("lowercase", errorMessage);
    }

    [Fact]
    public void Validate_NoDigit_ReturnsFalse()
    {
        var (isValid, errorMessage) = PasswordValidator.Validate("Password!!");
        Assert.False(isValid);
        Assert.Contains("digit", errorMessage);
    }

    [Fact]
    public void Validate_NoSpecialChar_ReturnsFalse()
    {
        var (isValid, errorMessage) = PasswordValidator.Validate("Password12");
        Assert.False(isValid);
        Assert.Contains("special character", errorMessage);
    }
}
