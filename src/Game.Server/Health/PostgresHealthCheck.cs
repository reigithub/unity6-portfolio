using Game.Server.Database;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Game.Server.Health;

public class PostgresHealthCheck : IHealthCheck
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PostgresHealthCheck(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("PostgreSQL connection failed.", ex));
        }
    }
}
