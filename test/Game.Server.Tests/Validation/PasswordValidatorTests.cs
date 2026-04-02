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

    // --- 境界値テスト ---

    [Fact]
    public void Validate_ExactlyMinLength_ReturnsTrue()
    {
        var (isValid, _) = PasswordValidator.Validate("Aa1!xxxx"); // 8文字ちょうど
        Assert.True(isValid);
    }

    [Fact]
    public void Validate_OneCharBelowMinLength_ReturnsFalse()
    {
        var (isValid, errorMessage) = PasswordValidator.Validate("Aa1!xxx"); // 7文字
        Assert.False(isValid);
        Assert.Contains("8 characters", errorMessage);
    }

    [Fact]
    public void Validate_OneAboveMinLength_ReturnsTrue()
    {
        var (isValid, _) = PasswordValidator.Validate("Aa1!xxxxx"); // 9文字
        Assert.True(isValid);
    }

    [Fact]
    public void Validate_MinimalPatternAllCategories_ReturnsTrue()
    {
        // 各カテゴリ1文字のみ + パディングで最小構成8文字
        var (isValid, _) = PasswordValidator.Validate("Aa1!aaaa");
        Assert.True(isValid);
    }
}
