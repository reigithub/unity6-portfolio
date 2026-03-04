using System.Text;
using Game.Library.Shared.RequestSigning;

namespace Game.Realtime.Tests.Shared;

/// <summary>
/// SessionTokenHelper の単体テスト。
/// HMAC トークンの生成・検証・有効期限・改ざん検知を検証する。
/// </summary>
public class SessionTokenHelperTests
{
    private readonly byte[] _secretKey = Encoding.UTF8.GetBytes("test-secret-key-for-session-token");

    #region CreateToken

    [Fact]
    public void CreateToken_ReturnsTokenWithDotSeparator()
    {
        var token = SessionTokenHelper.CreateToken(_secretKey, "user1", "match1");

        Assert.NotNull(token);
        Assert.Contains(".", token);
    }

    [Fact]
    public void CreateToken_ReturnsDifferentTokensForDifferentUsers()
    {
        var token1 = SessionTokenHelper.CreateToken(_secretKey, "user1", "match1");
        var token2 = SessionTokenHelper.CreateToken(_secretKey, "user2", "match1");

        Assert.NotEqual(token1, token2);
    }

    [Fact]
    public void CreateToken_ReturnsDifferentTokensForDifferentMatches()
    {
        var token1 = SessionTokenHelper.CreateToken(_secretKey, "user1", "match1");
        var token2 = SessionTokenHelper.CreateToken(_secretKey, "user1", "match2");

        Assert.NotEqual(token1, token2);
    }

    #endregion

    #region ParseAndVerify - Success

    [Fact]
    public void ParseAndVerify_ReturnsResult_WhenTokenIsValid()
    {
        var token = SessionTokenHelper.CreateToken(_secretKey, "user1", "match1");

        var result = SessionTokenHelper.ParseAndVerify(token, _secretKey);

        Assert.NotNull(result);
        Assert.Equal("user1", result!.UserId);
        Assert.Equal("match1", result.MatchId);
    }

    [Fact]
    public void ParseAndVerify_IssuedAt_IsRecentTimestamp()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var token = SessionTokenHelper.CreateToken(_secretKey, "user1", "match1");
        var after = DateTimeOffset.UtcNow.AddSeconds(1);

        var result = SessionTokenHelper.ParseAndVerify(token, _secretKey);

        Assert.NotNull(result);
        Assert.True(result!.IssuedAt >= before, "IssuedAt should be >= test start time");
        Assert.True(result.IssuedAt <= after, "IssuedAt should be <= test end time");
    }

    #endregion

    #region ParseAndVerify - Failure Cases

    [Fact]
    public void ParseAndVerify_ReturnsNull_WhenTokenIsNull()
    {
        var result = SessionTokenHelper.ParseAndVerify(null!, _secretKey);

        Assert.Null(result);
    }

    [Fact]
    public void ParseAndVerify_ReturnsNull_WhenTokenIsEmpty()
    {
        var result = SessionTokenHelper.ParseAndVerify("", _secretKey);

        Assert.Null(result);
    }

    [Fact]
    public void ParseAndVerify_ReturnsNull_WhenTokenHasNoDot()
    {
        var result = SessionTokenHelper.ParseAndVerify("nodottoken", _secretKey);

        Assert.Null(result);
    }

    [Fact]
    public void ParseAndVerify_ReturnsNull_WhenSignatureIsTampered()
    {
        var token = SessionTokenHelper.CreateToken(_secretKey, "user1", "match1");

        // 署名部分を改ざん
        var dotIndex = token.LastIndexOf('.');
        var tamperedToken = token.Substring(0, dotIndex) + ".0000000000000000000000000000000000000000000000000000000000000000";

        var result = SessionTokenHelper.ParseAndVerify(tamperedToken, _secretKey);

        Assert.Null(result);
    }

    [Fact]
    public void ParseAndVerify_ReturnsNull_WhenPayloadIsTampered()
    {
        var token = SessionTokenHelper.CreateToken(_secretKey, "user1", "match1");

        // ペイロード部分を改ざん（別ユーザーの Base64Url エンコード）
        var dotIndex = token.LastIndexOf('.');
        var signature = token.Substring(dotIndex);
        var fakePayload = Convert.ToBase64String(
            Encoding.UTF8.GetBytes("hacker|match1|9999999999"))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');
        var tamperedToken = fakePayload + signature;

        var result = SessionTokenHelper.ParseAndVerify(tamperedToken, _secretKey);

        Assert.Null(result);
    }

    [Fact]
    public void ParseAndVerify_ReturnsNull_WhenSecretKeyIsDifferent()
    {
        var token = SessionTokenHelper.CreateToken(_secretKey, "user1", "match1");
        var wrongKey = Encoding.UTF8.GetBytes("wrong-secret-key");

        var result = SessionTokenHelper.ParseAndVerify(token, wrongKey);

        Assert.Null(result);
    }

    [Fact]
    public void ParseAndVerify_ReturnsNull_WhenPayloadHasInvalidFormat()
    {
        // パイプ区切りが2つでない不正ペイロードを手動生成
        var payload = "onlyonepart";
        var payloadB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');
        var signature = HmacRequestSigner.ComputeSignature(_secretKey, payload);
        var token = $"{payloadB64}.{signature}";

        var result = SessionTokenHelper.ParseAndVerify(token, _secretKey);

        Assert.Null(result);
    }

    [Fact]
    public void ParseAndVerify_ReturnsNull_WhenTimestampIsNotNumeric()
    {
        var payload = "user1|match1|notanumber";
        var payloadB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');
        var signature = HmacRequestSigner.ComputeSignature(_secretKey, payload);
        var token = $"{payloadB64}.{signature}";

        var result = SessionTokenHelper.ParseAndVerify(token, _secretKey);

        Assert.Null(result);
    }

    #endregion

    #region Expiry

    [Fact]
    public void ParseAndVerify_ReturnsNull_WhenTokenIsExpired()
    {
        // DefaultExpiry (5分) を超過したタイムスタンプで手動トークン生成
        var expiredTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();
        var payload = $"user1|match1|{expiredTimestamp}";
        var payloadB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');
        var signature = HmacRequestSigner.ComputeSignature(_secretKey, payload);
        var token = $"{payloadB64}.{signature}";

        var result = SessionTokenHelper.ParseAndVerify(token, _secretKey);

        Assert.Null(result);
    }

    [Fact]
    public void DefaultExpiry_IsFiveMinutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(5), SessionTokenHelper.DefaultExpiry);
    }

    #endregion
}
