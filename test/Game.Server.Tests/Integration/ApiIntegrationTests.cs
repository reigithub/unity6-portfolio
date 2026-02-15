using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Game.Library.Shared.RequestSigning;
using Game.Server.Dto.Responses;
using Game.Server.Tests.Fixtures;

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
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task HealthEndpoint_Returns200()
    {
        var response = await _client.GetAsync("/api/health");
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
        using var authClient = _factory.CreateClient();
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
        using var unauthClient = _factory.CreateClient();
        var response = await unauthClient.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
        using var newClient = _factory.CreateClient();
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
        using var newClient = _factory.CreateClient();
        var loginResponse = await newClient.PostAsJsonAsync("/api/auth/login", new
        {
            UserId = linkData!.UserId,
            Password = "LinkPassword123!"
        });
        Assert.Equal(HttpStatusCode.BadRequest, loginResponse.StatusCode);
    }

    #region Helpers

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
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add(RequestSigningConstants.SignatureHeader, signature);
        request.Headers.Add(RequestSigningConstants.TimestampHeader, timestamp.ToString());
        request.Headers.Add(RequestSigningConstants.NonceHeader, nonce);

        using var client = _factory.CreateClient();
        return await client.SendAsync(request);
    }

    #endregion
}
