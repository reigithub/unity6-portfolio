using System.Text;
using Game.Library.Shared.RequestSigning;

namespace Game.Realtime.Tests.Shared;

/// <summary>
/// SessionTokenHelper の単体テスト。
/// MessagePack バイナリ形式トークンの生成・検証・有効期限・改ざん検知を検証する。
/// </summary>
public class SessionTokenHelperTests
{
    private readonly byte[] _secretKey = Encoding.UTF8.GetBytes("test-secret-key-for-session-token");

    #region CreateToken / CreateTokenBytes

    [Fact]
    public void CreateToken_ReturnsNonEmptyBase64String()
    {
        var token = SessionTokenHelper.CreateToken(_secretKey, "user1", "match1");

        Assert.NotNull(token);
        Assert.NotEmpty(token);
        // Base64 として有効かどうか確認
        var bytes = Convert.FromBase64String(token);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void CreateTokenBytes_ReturnsTokenWithin128Bytes()
    {
        var token = SessionTokenHelper.CreateTokenBytes(_secretKey, "user1", "match1");

        Assert.NotNull(token);
        Assert.True(token.Length <= 128, $"トークンサイズ {token.Length}B が Fusion 上限 128B を超えています");
    }

    [Fact]
    public void CreateTokenBytes_ReturnsTokenWithUuidUserId()
    {
        var userId = Guid.NewGuid().ToString(); // 36文字 UUID
        var matchId = $"match-{Guid.NewGuid():N}"; // 42文字

        var token = SessionTokenHelper.CreateTokenBytes(_secretKey, userId, matchId);

        Assert.NotNull(token);
        Assert.True(token.Length <= 128, $"UUID 形式 userId でもトークンサイズ {token.Length}B が 128B 以内であること");
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
        Assert.Equal("match1", result.SessionName);
    }

    [Fact]
    public void ParseAndVerify_IssuedAt_IsRecentTimestamp()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var token = SessionTokenHelper.CreateToken(_secretKey, "user1", "match1");
        var after = DateTimeOffset.UtcNow.AddSeconds(1);

        var result = SessionTokenHelper.ParseAndVerify(token, _secretKey);

        Assert.NotNull(result);
        Assert.True(result!.IssuedAt >= before, "IssuedAt はテスト開始時刻以降であること");
        Assert.True(result.IssuedAt <= after, "IssuedAt はテスト終了時刻以前であること");
    }

    [Fact]
    public void ParseAndVerifyBytes_ReturnsResult_WhenTokenBytesAreValid()
    {
        var tokenBytes = SessionTokenHelper.CreateTokenBytes(_secretKey, "user1", "match1");

        var result = SessionTokenHelper.ParseAndVerifyBytes(tokenBytes, _secretKey);

        Assert.NotNull(result);
        Assert.Equal("user1", result!.UserId);
        Assert.Equal("match1", result.SessionName);
    }

    [Fact]
    public void ParseAndVerify_AndParseAndVerifyBytes_ReturnSameResult()
    {
        var token = SessionTokenHelper.CreateToken(_secretKey, "user1", "match1");
        var tokenBytes = Convert.FromBase64String(token);

        var result1 = SessionTokenHelper.ParseAndVerify(token, _secretKey);
        var result2 = SessionTokenHelper.ParseAndVerifyBytes(tokenBytes, _secretKey);

        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(result1!.UserId, result2!.UserId);
        Assert.Equal(result1.SessionName, result2.SessionName);
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
    public void ParseAndVerify_ReturnsNull_WhenTokenIsInvalidBase64()
    {
        var result = SessionTokenHelper.ParseAndVerify("not-valid-base64!!!", _secretKey);

        Assert.Null(result);
    }

    [Fact]
    public void ParseAndVerifyBytes_ReturnsNull_WhenTokenIsNull()
    {
        var result = SessionTokenHelper.ParseAndVerifyBytes(null!, _secretKey);

        Assert.Null(result);
    }

    [Fact]
    public void ParseAndVerifyBytes_ReturnsNull_WhenTokenIsTooShort()
    {
        // 32B 以下はペイロードなしとして拒否される
        var result = SessionTokenHelper.ParseAndVerifyBytes(new byte[32], _secretKey);

        Assert.Null(result);
    }

    [Fact]
    public void ParseAndVerifyBytes_ReturnsNull_WhenSignatureIsTampered()
    {
        var tokenBytes = SessionTokenHelper.CreateTokenBytes(_secretKey, "user1", "match1");

        // 末尾 32B（署名）を改ざん
        var tampered = (byte[])tokenBytes.Clone();
        tampered[tampered.Length - 1] ^= 0xFF;

        var result = SessionTokenHelper.ParseAndVerifyBytes(tampered, _secretKey);

        Assert.Null(result);
    }

    [Fact]
    public void ParseAndVerifyBytes_ReturnsNull_WhenPayloadIsTampered()
    {
        var tokenBytes = SessionTokenHelper.CreateTokenBytes(_secretKey, "user1", "match1");

        // ペイロード先頭バイトを改ざん（署名と不一致になる）
        var tampered = (byte[])tokenBytes.Clone();
        tampered[0] ^= 0xFF;

        var result = SessionTokenHelper.ParseAndVerifyBytes(tampered, _secretKey);

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

    #endregion

    #region Expiry

    [Fact]
    public void ParseAndVerify_ReturnsNull_WhenTokenIsExpired()
    {
        // DefaultExpiry (5分) を超過したタイムスタンプを持つトークンを手動生成
        var expiredTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();

        // MessagePackWriter で手動パック
        var buffer = new System.Buffers.ArrayBufferWriter<byte>(128);
        var writer = new MessagePack.MessagePackWriter(buffer);
        writer.WriteArrayHeader(3);
        writer.Write("user1");
        writer.Write("match1");
        writer.Write(expiredTimestamp);
        writer.Flush();
        var payloadBytes = buffer.WrittenMemory.ToArray();

        // HMAC 署名を公開 API 経由で生成（HMAC-SHA256 の生バイト列を取得）
        // HmacRequestSigner.ComputeSignatureBytes は internal のため
        // 同じ HMAC を別経路で計算する
        byte[] signature;
        using (var hmac = new System.Security.Cryptography.HMACSHA256(_secretKey))
        {
            signature = hmac.ComputeHash(payloadBytes);
        }

        var tokenBytes = new byte[payloadBytes.Length + signature.Length];
        Buffer.BlockCopy(payloadBytes, 0, tokenBytes, 0, payloadBytes.Length);
        Buffer.BlockCopy(signature, 0, tokenBytes, payloadBytes.Length, signature.Length);
        var token = Convert.ToBase64String(tokenBytes);

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
