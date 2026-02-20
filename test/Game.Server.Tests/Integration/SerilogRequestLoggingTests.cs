using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Game.Server.Tests.Integration;

/// <summary>
/// UseSerilogRequestLogging が実際に HTTP リクエストログを出力することを検証する統合テスト。
/// Testcontainers (PostgreSQL) は不要 — health エンドポイントのみ使用する。
/// </summary>
public class SerilogRequestLoggingTests : IAsyncLifetime
{
    private readonly TestSink _sink = new();
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                // Development だと FluentMigrator の自動マイグレーションが走り
                // ダミー接続文字列では DB 接続に失敗するため Testing を使用
                builder.UseEnvironment("Testing");

                builder.ConfigureServices(services =>
                {
                    // Serilog をテスト用シンクで上書き
                    services.AddSerilog(lc => lc
                        .MinimumLevel.Debug()
                        .WriteTo.Sink(_sink));

                    // DB マイグレーションをスキップするためのダミー設定
                    TestServiceOverrides.Apply(services);
                });
            });

        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task HealthRequest_ProducesSerilogRequestLog()
    {
        // Arrange — シンクをクリア
        _sink.Clear();

        // Act
        var response = await _client.GetAsync("/health");

        // Assert — Serilog のリクエストログが記録されていること
        // （ヘルスチェックのステータスは問わない — ログ出力の有無を検証する）
        var requestLogEvent = _sink.Events
            .FirstOrDefault(e =>
                e.MessageTemplate.Text.Contains("HTTP {RequestMethod} {RequestPath}"));

        Assert.NotNull(requestLogEvent);

        // 主要プロパティが含まれていること
        Assert.True(requestLogEvent.Properties.ContainsKey("RequestMethod"));
        Assert.True(requestLogEvent.Properties.ContainsKey("RequestPath"));
        Assert.True(requestLogEvent.Properties.ContainsKey("StatusCode"));
        Assert.True(requestLogEvent.Properties.ContainsKey("Elapsed"));

        // 値の検証
        Assert.Equal("\"GET\"", requestLogEvent.Properties["RequestMethod"].ToString());
        Assert.Contains("/health", requestLogEvent.Properties["RequestPath"].ToString());

        var statusCode = (int)response.StatusCode;
        Assert.Equal(
            statusCode.ToString(),
            requestLogEvent.Properties["StatusCode"].ToString());
    }

    /// <summary>
    /// ログイベントをメモリに蓄積するテスト用シンク
    /// </summary>
    private sealed class TestSink : ILogEventSink
    {
        private readonly ConcurrentBag<LogEvent> _events = new();

        public IReadOnlyCollection<LogEvent> Events => _events;

        public void Emit(LogEvent logEvent)
        {
            _events.Add(logEvent);
        }

        public void Clear()
        {
            _events.Clear();
        }
    }
}
