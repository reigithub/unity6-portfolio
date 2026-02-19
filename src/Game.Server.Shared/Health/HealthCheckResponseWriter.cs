using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Game.Server.Shared.Health;

public static class HealthCheckResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var result = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
            }),
            totalDuration = $"{report.TotalDuration.TotalMilliseconds:F1}ms",
        };

        return context.Response.WriteAsJsonAsync(result);
    }
}
