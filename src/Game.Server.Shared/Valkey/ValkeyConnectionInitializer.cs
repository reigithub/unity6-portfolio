using System.Net.Security;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Game.Server.Shared.Valkey;

/// <summary>
/// IHostedService として Valkey/Redis の IAM 認証付き接続を非同期で確立し、
/// トークンリフレッシュタイマーのライフサイクルを管理する。
/// </summary>
public sealed class ValkeyConnectionInitializer : IHostedService, IDisposable
{
    private static readonly TimeSpan TokenRefreshInterval = TimeSpan.FromMinutes(4);

    private readonly ConfigurationOptions _options;
    private readonly ILogger<ValkeyConnectionInitializer> _logger;

    private IConnectionMultiplexer? _multiplexer;
    private Timer? _tokenRefreshTimer;
    private GoogleCredential? _credential;

    public ValkeyConnectionInitializer(ConfigurationOptions options, ILogger<ValkeyConnectionInitializer> logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// 初期化済みの接続インスタンス。StartAsync 完了前にアクセスすると例外をスローする。
    /// </summary>
    public IConnectionMultiplexer Multiplexer
    {
        get
        {
            return _multiplexer
                ?? throw new InvalidOperationException(
                    "ValkeyConnectionInitializer has not been started. " +
                    "Ensure the hosted service has completed StartAsync before resolving IConnectionMultiplexer.");
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _credential = await GoogleCredential.GetApplicationDefaultAsync(cancellationToken);
        var token = await _credential.UnderlyingCredential.GetAccessTokenForRequestAsync(cancellationToken: cancellationToken);

        _options.User = "default";
        _options.Password = token;

        // GCP Memorystore の内部 CA 証明書を信頼する
        _options.CertificateValidation += (_, _, _, errors) =>
            errors is SslPolicyErrors.None or SslPolicyErrors.RemoteCertificateChainErrors;

        _multiplexer = await ConnectionMultiplexer.ConnectAsync(_options);
        _logger.LogInformation("Connected to Valkey/Redis with IAM authentication");

        // トークンリフレッシュタイマー（4分間隔、トークン有効期限は1時間）
        _tokenRefreshTimer = new Timer(
            callback => _ = RefreshTokenAsync(),
            null,
            TokenRefreshInterval,
            TokenRefreshInterval);

        _multiplexer.ConnectionFailed += (_, args) =>
            _logger.LogWarning("Valkey connection failed: {FailureType}", args.FailureType);
        _multiplexer.ConnectionRestored += (_, _) =>
            _logger.LogInformation("Valkey connection restored");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_tokenRefreshTimer is not null)
        {
            await _tokenRefreshTimer.DisposeAsync();
            _tokenRefreshTimer = null;
        }

        if (_multiplexer is not null)
        {
            await _multiplexer.CloseAsync();
            _multiplexer.Dispose();
            _multiplexer = null;
        }
    }

    public void Dispose()
    {
        _tokenRefreshTimer?.Dispose();
        _multiplexer?.Dispose();
    }

    private async Task RefreshTokenAsync()
    {
        try
        {
            var newToken = await _credential!.UnderlyingCredential.GetAccessTokenForRequestAsync();
            foreach (var server in _multiplexer!.GetServers())
            {
                await server.ExecuteAsync("AUTH", "default", newToken);
            }

            _logger.LogDebug("Valkey IAM token refreshed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh Valkey IAM token");
        }
    }
}
