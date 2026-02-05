using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
    public async Task Register_And_Login_Flow()
    {
        string name = "TestUser_" + Guid.NewGuid().ToString()[..8];

        // Register
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            UserName = name,
            Password = "TestPassword123!",
        });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginData = await registerResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginData?.Token);

        // Use token to get user info
        using var authClient = _factory.CreateClient();
        authClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginData.Token);

        var meResponse = await authClient.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateName_Returns409()
    {
        string name = "DupUser_" + Guid.NewGuid().ToString()[..8];

        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            UserName = name,
            Password = "Password123!",
        });

        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            UserName = name,
            Password = "Password456!",
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
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
        string token = await RegisterAndGetToken("RankPlayer_" + Guid.NewGuid().ToString()[..8]);

        using var authClient = _factory.CreateClient();
        authClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var scoreResponse = await authClient.PostAsJsonAsync("/api/scores", new
        {
            StageId = 1,
            Score = 5000,
            ClearTime = 120.5f,
            GameMode = "Survivor",
            WaveReached = 10,
            EnemiesDefeated = 50,
        });
        Assert.Equal(HttpStatusCode.Created, scoreResponse.StatusCode);

        var rankingResponse = await _client.GetAsync("/api/rankings/Survivor/1");
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

        // 2. Link to email
        using var authClient = _factory.CreateClient();
        authClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", guestData.Token);

        var linkResponse = await authClient.PostAsJsonAsync("/api/auth/link/email", new
        {
            Email = $"link-{Guid.NewGuid():N}@example.com",
            Password = "LinkPassword123!"
        });
        Assert.Equal(HttpStatusCode.OK, linkResponse.StatusCode);

        var linkData = await linkResponse.Content.ReadFromJsonAsync<AccountLinkResponse>();
        Assert.NotNull(linkData);
        Assert.Equal("Email", linkData.AuthType);
        Assert.NotEmpty(linkData.Token);

        // 3. Unlink back to guest
        using var linkedClient = _factory.CreateClient();
        linkedClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", linkData.Token);

        var unlinkResponse = await linkedClient.DeleteAsync(
            "/api/auth/link/email?deviceFingerprint=unlink-device-fingerprint-0123456789abcdef");
        Assert.Equal(HttpStatusCode.OK, unlinkResponse.StatusCode);

        var unlinkData = await unlinkResponse.Content.ReadFromJsonAsync<AccountLinkResponse>();
        Assert.NotNull(unlinkData);
        Assert.Equal("Guest", unlinkData.AuthType);
        Assert.Null(unlinkData.Email);
    }

    private async Task<string> RegisterAndGetToken(string userName)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            UserName = userName,
            Password = "Password123!",
        });
        var data = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return data!.Token;
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

        // 2. Issue transfer password
        using var authClient = _factory.CreateClient();
        authClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", guestData.Token);

        var issueResponse = await authClient.PostAsync("/api/auth/transfer-password", null);
        Assert.Equal(HttpStatusCode.OK, issueResponse.StatusCode);

        var transferData = await issueResponse.Content.ReadFromJsonAsync<TransferPasswordResponse>();
        Assert.NotNull(transferData);
        Assert.NotEmpty(transferData.TransferPassword);
        Assert.Equal(12, transferData.TransferPassword.Length);
        Assert.Equal(guestData.UserId, transferData.UserId);

        // 3. Login with transfer password from another "device"
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

        // 2. Link to email
        using var authClient = _factory.CreateClient();
        authClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", guestData!.Token);

        var linkResponse = await authClient.PostAsJsonAsync("/api/auth/link/email", new
        {
            Email = $"transfer-block-{Guid.NewGuid():N}@example.com",
            Password = "LinkPassword123!"
        });
        Assert.Equal(HttpStatusCode.OK, linkResponse.StatusCode);

        var linkData = await linkResponse.Content.ReadFromJsonAsync<AccountLinkResponse>();

        // 3. Try to issue transfer password (should fail for email users)
        using var linkedClient = _factory.CreateClient();
        linkedClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", linkData!.Token);

        var issueResponse = await linkedClient.PostAsync("/api/auth/transfer-password", null);
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

        // 2. Link to email
        using var authClient = _factory.CreateClient();
        authClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", guestData!.Token);

        var linkResponse = await authClient.PostAsJsonAsync("/api/auth/link/email", new
        {
            Email = $"userid-block-{Guid.NewGuid():N}@example.com",
            Password = "LinkPassword123!"
        });
        Assert.Equal(HttpStatusCode.OK, linkResponse.StatusCode);

        var linkData = await linkResponse.Content.ReadFromJsonAsync<AccountLinkResponse>();

        // 3. Try to login with User ID (should fail for email users)
        using var newClient = _factory.CreateClient();
        var loginResponse = await newClient.PostAsJsonAsync("/api/auth/login", new
        {
            UserId = linkData!.UserId,
            Password = "LinkPassword123!"
        });
        Assert.Equal(HttpStatusCode.BadRequest, loginResponse.StatusCode);
    }
}
