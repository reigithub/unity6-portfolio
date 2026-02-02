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
            DisplayName = name,
            Password = "TestPassword123",
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
            DisplayName = name,
            Password = "Password123",
        });

        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            DisplayName = name,
            Password = "Password456",
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

    private async Task<string> RegisterAndGetToken(string displayName)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            DisplayName = displayName,
            Password = "Password123",
        });
        var data = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return data!.Token;
    }
}
