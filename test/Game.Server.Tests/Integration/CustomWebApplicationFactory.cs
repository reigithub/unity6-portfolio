using Game.Server.Database;
using Game.Server.Services.Interfaces;
using Game.Server.Tests.Fixtures;
using Game.Server.Validation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using StackExchange.Redis;

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

            // Replace ISurvivorScoreValidator with a mock so that tests
            // don't require master data for score validation.
            var mockValidation = new Mock<ISurvivorScoreValidator>();
            services.RemoveAll<ISurvivorScoreValidator>();
            services.AddSingleton(mockValidation.Object);

            // Replace IConnectionMultiplexer with a mock so that tests
            // don't require a running Valkey/Redis instance.
            var mockDb = new Mock<IDatabase>();
            mockDb.Setup(d => d.PingAsync(It.IsAny<CommandFlags>()))
                .ReturnsAsync(TimeSpan.FromMilliseconds(1));
            var mockRedis = new Mock<IConnectionMultiplexer>();
            mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(mockDb.Object);
            services.RemoveAll<IConnectionMultiplexer>();
            services.AddSingleton(mockRedis.Object);

            // Replace IEmailService with a mock so that tests
            // don't require Resend API credentials.
            var mockEmailService = new Mock<IEmailService>();
            mockEmailService
                .Setup(e => e.SendVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            mockEmailService
                .Setup(e => e.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            services.RemoveAll<IEmailService>();
            services.AddSingleton(mockEmailService.Object);
        });

        builder.UseEnvironment("Development");
    }
}
