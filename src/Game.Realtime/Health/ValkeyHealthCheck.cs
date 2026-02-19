using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Game.Realtime.Health;

public class ValkeyHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _valkey;

    public ValkeyHealthCheck(IConnectionMultiplexer valkey) => _valkey = valkey;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _valkey.GetDatabase();
            var latency = await db.PingAsync();
            return HealthCheckResult.Healthy($"Ping: {latency.TotalMilliseconds:F1}ms");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Valkey connection failed.", ex);
        }
    }
}
