using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Game.Library.Shared.RequestSigning;
using Game.Library.Shared.Dto;
using Game.Server.Configuration;
using Game.Server.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Game.Server.Tests.Integration;

[Collection("Database")]
public class ApiIntegrationTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _postgres;
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public ApiIntegrationTests(PostgresContainerFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        await _postgres.ResetUserDataAsync();
        _factory = new CustomWebApplicationFactory(_postgres.ConnectionString);
        _client = CreateJsonClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task HealthEndpoint_Returns200()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GuestLogin_And_GetUserInfo_Flow()
    {
        // Guest login
        var guestResponse = await _client.PostAsJsonAsync("/api/auth/guest", new
        {
            DeviceFingerprint = "test-device-" + Guid.NewGuid().ToString("N")
        });
        Assert.Equal(HttpStatusCode.Created, guestResponse.StatusCode);

        var loginData = await guestResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginData?.Token);
        Assert.True(loginData.IsNewUser);

        // Use token to get user info
        using var authClient = CreateJsonClient();
        authClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginData.Token);

        var meResponse = await authClient.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
    }

    [Fact]
    public async Task GuestLogin_SameDevice_ReturnsSameUser()
    {
        string deviceFingerprint = "same-device-" + Guid.NewGuid().ToString("N");

        // First login
        var firstResponse = await _client.PostAsJsonAsync("/api/auth/guest", new
        {
            DeviceFingerprint = deviceFingerprint
        });
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var firstData = await firstResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.True(firstData?.IsNewUser);

        // Second login with same device
        var secondResponse = await _client.PostAsJsonAsync("/api/auth/guest", new
        {
            DeviceFingerprint = deviceFingerprint
        });
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var secondData = await secondResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(secondData?.IsNewUser);

        // Should be the same user
        Assert.Equal(firstData?.UserId, secondData?.UserId);
    }

    [Fact]
    public async Task UnauthorizedEndpoint_Returns401()
    {
        using var unauthClient = CreateJsonClient();
        var response = await unauthClient.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DsEndpoint_WithoutSignature_Returns401()
    {
        // [DsSignature] が付いている DS 経路に無署名で POST しても 401 が返ることを確認する
        // (attribute ベース middleware が DS 経路を正しく fail-closed で保護している regression test)
        using var unauthClient = CreateJsonClient();
        var response = await unauthClient.PostAsJsonAsync("/api/unity-server/register", new
        {
            DsId = "test-ds",
            Address = "127.0.0.1",
            GamePort = 7777,
            HealthPort = 7778,
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutJwt_ReturnsNewTokenPair()
    {
        // Arrange: ゲストログインで refresh token を取得
        var guestResponse = await _client.PostAsJsonAsync("/api/auth/guest", new
        {
            DeviceFingerprint = "refresh-test-" + Guid.NewGuid().ToString("N")
        });
        guestResponse.EnsureSuccessStatusCode();
        var initialLogin = await guestResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(initialLogin?.RefreshToken);

        // Act: Authorization ヘッダ無しで refresh
        using var clientWithoutJwt = CreateJsonClient();
        var response = await clientWithoutJwt.PostAsJsonAsync("/api/auth/refresh", new
        {
            RefreshToken = initialLogin.RefreshToken
        });

        // Assert: 200 OK + 新しい token pair
        // Note: access token (JWT) は exp claim が秒精度で 1 秒以内の連続発行は同一文字列に
        // なりうるため、rotation 確認は refresh_token 側の NotEqual で行う
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var newLogin = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(newLogin?.Token);
        Assert.NotNull(newLogin.RefreshToken);
        Assert.NotEqual(initialLogin.RefreshToken, newLogin.RefreshToken);
        Assert.Equal(initialLogin.UserId, newLogin.UserId);
    }

    [Fact]
    public async Task Refresh_WithExpiredJwt_ReturnsNewTokenPair()
    {
        // Arrange: ゲストログインで refresh token を取得
        var guestResponse = await _client.PostAsJsonAsync("/api/auth/guest", new
        {
            DeviceFingerprint = "refresh-expired-jwt-" + Guid.NewGuid().ToString("N")
        });
        guestResponse.EnsureSuccessStatusCode();
        var initialLogin = await guestResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(initialLogin?.RefreshToken);

        // 期限切れ JWT を手動生成 (JwtSettings から Secret を取得)
        var jwtSettings = _factory.Services.GetRequiredService<IOptions<JwtSettings>>().Value;
        var expiredJwt = GenerateExpiredJwt(jwtSettings, initialLogin.UserId);

        using var client = CreateJsonClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", expiredJwt);

        // Act: 期限切れ JWT + 有効な refresh token で refresh
        // (critical bug の直接再現経路: Header あり + 期限切れ JWT pipeline)
        var response = await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            RefreshToken = initialLogin.RefreshToken
        });

        // Assert: 200 OK — JWT 期限切れでも refresh 成功 (regression guard)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var newLogin = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(newLogin?.Token);
        Assert.NotEqual(expiredJwt, newLogin.Token);
    }

    [Fact]
    public async Task Refresh_InvalidRefreshToken_Returns401()
    {
        // 完全に無効な refresh token を middleware 経由で送る
        using var client = CreateJsonClient();
        var response = await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            RefreshToken = "completely-bogus-" + Guid.NewGuid().ToString("N")
        });

        // Assert: middleware 経由で service に到達し INVALID_REFRESH_TOKEN → 401
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_SameTokenTwice_SecondCallFails()
    {
        // Arrange: ゲストログインで refresh token を取得
        var guestResponse = await _client.PostAsJsonAsync("/api/auth/guest", new
        {
            DeviceFingerprint = "refresh-rotation-test-" + Guid.NewGuid().ToString("N")
        });
        guestResponse.EnsureSuccessStatusCode();
        var initialLogin = await guestResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(initialLogin?.RefreshToken);

        using var client = CreateJsonClient();

        // Act 1: 1 回目の refresh は成功 (新 refresh token 発行、旧 refresh token 無効化)
        var firstResponse = await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            RefreshToken = initialLogin.RefreshToken
        });
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        // Act 2: 2 回目に同じ (rotation で無効化済みの) refresh token を再使用 → 失敗
        var secondResponse = await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            RefreshToken = initialLogin.RefreshToken
        });

        // Assert: 2 回目は 401 (replay 防御確認)
        Assert.Equal(HttpStatusCode.Unauthorized, secondResponse.StatusCode);
    }

    [Fact]
    public async Task SubmitScore_And_GetRanking()
    {
        var (token, signingKey) = await GuestLoginAndGetTokenWithKey();

        var scoreBody = new
        {
            StageId = 1,
            Score = 5000,
            ClearTime = 120.5f,
            WaveReached = 10,
            EnemiesDefeated = 50,
        };

        var scoreResponse = await PostSignedAsync(token, signingKey, "/api/survivor/scores", scoreBody);
        Assert.Equal(HttpStatusCode.Created, scoreResponse.StatusCode);

        var rankingResponse = await _client.GetAsync("/api/survivor/rankings/1");
        Assert.Equal(HttpStatusCode.OK, rankingResponse.StatusCode);
    }

    [Fact]
    public async Task LinkEmail_And_UnlinkEmail_Flow()
    {
        // 1. Guest login
        var guestResponse = await _client.PostAsJsonAsync("/api/auth/guest", new
        {
            DeviceFingerprint = "link-test-device-" + Guid.NewGuid().ToString("N")
        });
        Assert.Equal(HttpStatusCode.Created, guestResponse.StatusCode);

        var guestData = await guestResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(guestData?.Token);

        // 2. Link to email (signed request)
        var linkBody = new
        {
            Email = $"link-{Guid.NewGuid():N}@example.com",
            Password = "LinkPassword123!"
        };

        var linkResponse = await PostSignedAsync(guestData.Token, guestData.SigningKey, "/api/auth/link/email", linkBody);
        Assert.Equal(HttpStatusCode.OK, linkResponse.StatusCode);

        var linkData = await linkResponse.Content.ReadFromJsonAsync<AccountLinkResponse>();
        Assert.NotNull(linkData);
        Assert.Equal("Email", linkData.AuthType);
        Assert.NotEmpty(linkData.Token);

        // 3. Unlink back to guest (DELETE with signing)
        var unlinkResponse = await DeleteSignedAsync(
            linkData.Token, linkData.SigningKey,
            "/api/auth/link/email?deviceFingerprint=unlink-device-fingerprint-0123456789abcdef");
        Assert.Equal(HttpStatusCode.OK, unlinkResponse.StatusCode);

        var unlinkData = await unlinkResponse.Content.ReadFromJsonAsync<AccountLinkResponse>();
        Assert.NotNull(unlinkData);
        Assert.Equal("Guest", unlinkData.AuthType);
        Assert.Null(unlinkData.Email);
    }

    [Fact]
    public async Task IssueTransferPassword_And_Login_Flow()
    {
        // 1. Guest login
        var guestResponse = await _client.PostAsJsonAsync("/api/auth/guest", new
        {
            DeviceFingerprint = "transfer-test-device-" + Guid.NewGuid().ToString("N")
        });
        Assert.Equal(HttpStatusCode.Created, guestResponse.StatusCode);

        var guestData = await guestResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(guestData?.Token);

        // 2. Issue transfer password (signed request)
        var issueResponse = await PostSignedAsync(guestData.Token, guestData.SigningKey, "/api/auth/transfer-password", new { });
        Assert.Equal(HttpStatusCode.OK, issueResponse.StatusCode);

        var transferData = await issueResponse.Content.ReadFromJsonAsync<TransferPasswordResponse>();
        Assert.NotNull(transferData);
        Assert.NotEmpty(transferData.TransferPassword);
        Assert.Equal(12, transferData.TransferPassword.Length);
        Assert.Equal(guestData.UserId, transferData.UserId);

        // 3. Login with transfer password from another "device" (exempt endpoint, no signing needed)
        using var newClient = CreateJsonClient();
        var loginResponse = await newClient.PostAsJsonAsync("/api/auth/login", new
        {
            UserId = transferData.UserId,
            Password = transferData.TransferPassword
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginData = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginData);
        Assert.Equal(transferData.UserId, loginData.UserId);
        Assert.NotEmpty(loginData.Token);
    }

    [Fact]
    public async Task IssueTransferPassword_EmailUser_ReturnsBadRequest()
    {
        // 1. Guest login
        var guestResponse = await _client.PostAsJsonAsync("/api/auth/guest", new
        {
            DeviceFingerprint = "transfer-email-test-" + Guid.NewGuid().ToString("N")
        });
        Assert.Equal(HttpStatusCode.Created, guestResponse.StatusCode);

        var guestData = await guestResponse.Content.ReadFromJsonAsync<LoginResponse>();

        // 2. Link to email (signed request)
        var linkBody = new
        {
            Email = $"transfer-block-{Guid.NewGuid():N}@example.com",
            Password = "LinkPassword123!"
        };
        var linkResponse = await PostSignedAsync(guestData!.Token, guestData.SigningKey, "/api/auth/link/email", linkBody);
        Assert.Equal(HttpStatusCode.OK, linkResponse.StatusCode);

        var linkData = await linkResponse.Content.ReadFromJsonAsync<AccountLinkResponse>();

        // 3. Try to issue transfer password (should fail for email users, signed request)
        var issueResponse = await PostSignedAsync(linkData!.Token, linkData.SigningKey, "/api/auth/transfer-password", new { });
        Assert.Equal(HttpStatusCode.BadRequest, issueResponse.StatusCode);
    }

    [Fact]
    public async Task UserIdLogin_NonGuestUser_ReturnsBadRequest()
    {
        // 1. Guest login
        var guestResponse = await _client.PostAsJsonAsync("/api/auth/guest", new
        {
            DeviceFingerprint = "userid-login-test-" + Guid.NewGuid().ToString("N")
        });
        Assert.Equal(HttpStatusCode.Created, guestResponse.StatusCode);

        var guestData = await guestResponse.Content.ReadFromJsonAsync<LoginResponse>();

        // 2. Link to email (signed request)
        var linkBody = new
        {
            Email = $"userid-block-{Guid.NewGuid():N}@example.com",
            Password = "LinkPassword123!"
        };
        var linkResponse = await PostSignedAsync(guestData!.Token, guestData.SigningKey, "/api/auth/link/email", linkBody);
        Assert.Equal(HttpStatusCode.OK, linkResponse.StatusCode);

        var linkData = await linkResponse.Content.ReadFromJsonAsync<AccountLinkResponse>();

        // 3. Try to login with User ID (should fail for email users, exempt endpoint)
        using var newClient = CreateJsonClient();
        var loginResponse = await newClient.PostAsJsonAsync("/api/auth/login", new
        {
            UserId = linkData!.UserId,
            Password = "LinkPassword123!"
        });
        Assert.Equal(HttpStatusCode.BadRequest, loginResponse.StatusCode);
    }

    // --- Email認証フロー ---

    [Fact]
    public async Task EmailLogin_ValidCredentials_Returns200WithToken()
    {
        var (email, password) = await CreateEmailUserAsync();

        var response = await _client.PostAsJsonAsync("/api/auth/email/login", new
        {
            Email = email,
            Password = password
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(data);
        Assert.NotEmpty(data.Token);
    }

    [Fact]
    public async Task EmailLogin_WrongPassword_Returns401()
    {
        var (email, _) = await CreateEmailUserAsync();

        var response = await _client.PostAsJsonAsync("/api/auth/email/login", new
        {
            Email = email,
            Password = "WrongPassword999!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task VerifyEmail_InvalidToken_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/email/verify", new
        {
            Token = "invalid-verification-token"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_ExistingEmail_Returns200()
    {
        var (email, _) = await CreateEmailUserAsync();

        var response = await _client.PostAsJsonAsync("/api/auth/email/forgot-password", new
        {
            Email = email
        });

        // 情報漏洩防止のため、メール存在有無に関わらず常に200
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_NonExistentEmail_Returns200()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/email/forgot-password", new
        {
            Email = "nobody-exists@example.com"
        });

        // 情報漏洩防止のため常に200
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/email/reset-password", new
        {
            Token = "invalid-reset-token",
            NewPassword = "NewPassword123!"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- ユーザー更新 ---

    [Fact]
    public async Task UpdateMe_ValidRequest_Returns200WithUpdatedUser()
    {
        var (token, signingKey) = await GuestLoginAndGetTokenWithKey();

        var response = await PutSignedAsync(token, signingKey, "/api/users/me", new
        {
            UserName = "UpdatedName"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMe_Unauthorized_Returns401()
    {
        using var unauthClient = CreateJsonClient();
        var response = await unauthClient.PutAsJsonAsync("/api/users/me", new
        {
            UserName = "ShouldFail"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #region Helpers

    /// <summary>
    /// Accept: application/json をデフォルトヘッダーに設定した HttpClient を生成する。
    /// サーバーは MessagePack をデフォルト OutputFormatter としているため、
    /// JSON レスポンスが必要なテストでは明示的に Accept ヘッダーを指定する必要がある。
    /// </summary>
    private HttpClient CreateJsonClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    /// <summary>
    /// テスト用に期限切れ JWT を手動生成する。
    /// Refresh endpoint の "JWT 期限切れでも refresh が動作する" critical bug regression test 用。
    /// </summary>
    private static string GenerateExpiredJwt(JwtSettings settings, string userId)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-10),
            expires: DateTime.UtcNow.AddMinutes(-5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    private async Task<(string Token, string SigningKey)> GuestLoginAndGetTokenWithKey()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/guest", new
        {
            DeviceFingerprint = "score-test-device-" + Guid.NewGuid().ToString("N")
        });
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return (data!.Token, data.SigningKey);
    }

    /// <summary>
    /// 署名付き POST リクエストを送信する
    /// </summary>
    private Task<HttpResponseMessage> PostSignedAsync<T>(string token, string signingKey, string path, T body)
    {
        return SendSignedAsync(HttpMethod.Post, token, signingKey, path, body);
    }

    /// <summary>
    /// 署名付き PUT リクエストを送信する
    /// </summary>
    private Task<HttpResponseMessage> PutSignedAsync<T>(string token, string signingKey, string path, T body)
    {
        return SendSignedAsync(HttpMethod.Put, token, signingKey, path, body);
    }

    /// <summary>
    /// ゲストログイン → メール連携 → Email/Password を返すヘルパー
    /// </summary>
    private async Task<(string Email, string Password)> CreateEmailUserAsync()
    {
        var email = $"email-test-{Guid.NewGuid():N}@example.com";
        var password = "TestPassword123!";

        // 1. Guest login
        var guestResponse = await _client.PostAsJsonAsync("/api/auth/guest", new
        {
            DeviceFingerprint = "email-flow-" + Guid.NewGuid().ToString("N")
        });
        guestResponse.EnsureSuccessStatusCode();
        var guestData = await guestResponse.Content.ReadFromJsonAsync<LoginResponse>();

        // 2. Link email (signed)
        var linkResponse = await PostSignedAsync(guestData!.Token, guestData.SigningKey, "/api/auth/link/email", new
        {
            Email = email,
            Password = password
        });
        linkResponse.EnsureSuccessStatusCode();

        return (email, password);
    }

    /// <summary>
    /// 署名付き DELETE リクエストを送信する
    /// </summary>
    private Task<HttpResponseMessage> DeleteSignedAsync(string token, string signingKey, string path)
    {
        return SendSignedAsync<object?>(HttpMethod.Delete, token, signingKey, path, null);
    }

    /// <summary>
    /// 署名付き HTTP リクエストを送信する
    /// </summary>
    private async Task<HttpResponseMessage> SendSignedAsync<T>(HttpMethod method, string token, string signingKey, string path, T? body)
    {
        var userKey = Convert.FromBase64String(signingKey);

        byte[] bodyBytes;
        string? jsonBody = null;
        if (body != null)
        {
            jsonBody = JsonSerializer.Serialize(body, WebJsonOptions);
            bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
        }
        else
        {
            bodyBytes = Array.Empty<byte>();
        }

        // パスからクエリストリングを除去して署名対象とする
        var signPath = path.Contains('?') ? path[..path.IndexOf('?')] : path;

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var nonce = Guid.NewGuid().ToString();
        var canonicalString = HmacRequestSigner.BuildCanonicalString(method.Method, signPath, timestamp, nonce, bodyBytes);
        var signature = HmacRequestSigner.ComputeSignature(userKey, canonicalString);

        using var request = new HttpRequestMessage(method, path);
        if (jsonBody != null)
        {
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        }
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add(RequestSigningConstants.SignatureHeader, signature);
        request.Headers.Add(RequestSigningConstants.TimestampHeader, timestamp.ToString());
        request.Headers.Add(RequestSigningConstants.NonceHeader, nonce);

        using var client = _factory.CreateClient();
        return await client.SendAsync(request);
    }

    #endregion
}
