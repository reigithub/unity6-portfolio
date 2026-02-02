using Game.Server.Database;
using Game.Server.Services.Interfaces;
using Game.Server.Tests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Game.Server.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public CustomWebApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _connectionString,
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbConnectionFactory>();
            services.AddSingleton<IDbConnectionFactory>(
                new TestDbConnectionFactory(_connectionString));

            // Replace IMasterDataService with a mock so that tests
            // don't require a physical masterdata.bytes file.
            var mockMasterData = new Mock<IMasterDataService>();
            services.RemoveAll<IMasterDataService>();
            services.AddSingleton(mockMasterData.Object);
        });

        builder.UseEnvironment("Development");
    }
}
