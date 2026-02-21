using System.Security.Cryptography;
using System.Text;
using Game.Library.Shared.RequestSigning;
using Game.Server.Configuration;
using Game.Server.Shared.Extensions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Game.Server.Middleware;

public class RequestSigningMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestSigningMiddleware> _logger;
    private readonly byte[] _serverSecret;
    private readonly bool _enabled;

    private static readonly string[] ExemptPaths = new[]
    {
        "/api/auth/guest",
        "/api/auth/login",
        "/api/auth/email/login",
        "/api/auth/email/forgot-password",
        "/api/auth/email/reset-password",
        "/api/auth/email/verify",
    };

    public RequestSigningMiddleware(
        RequestDelegate next,
        IOptions<RequestSigningSettings> settings,
        ILogger<RequestSigningMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _serverSecret = Encoding.UTF8.GetBytes(settings.Value.SecretKey);
        _enabled = settings.Value.Enabled;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_enabled || !RequiresSignatureVerification(context.Request))
        {
            await _next(context);
            return;
        }

        // JWT から userId を取得
        var userId = context.User?.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("No authenticated user for signed request: {Path}", context.Request.Path);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "UNAUTHORIZED", message = "Authentication required for signed requests." });
            return;
        }

        // ヘッダー存在チェック
        if (!context.Request.Headers.TryGetValue(RequestSigningConstants.SignatureHeader, out var signature) ||
            !context.Request.Headers.TryGetValue(RequestSigningConstants.TimestampHeader, out var timestampStr) ||
            !context.Request.Headers.TryGetValue(RequestSigningConstants.NonceHeader, out var nonce))
        {
            _logger.LogWarning("Request signing headers missing for {Path}", context.Request.Path);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "UNAUTHORIZED", message = "Request signature required." });
            return;
        }

        // タイムスタンプ解析
        if (!long.TryParse(timestampStr, out var timestamp))
        {
            _logger.LogWarning("Invalid timestamp format: {Timestamp}", timestampStr.ToString());
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "UNAUTHORIZED", message = "Invalid request signature." });
            return;
        }

        // タイムスタンプ有効期限チェック
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - timestamp) > RequestSigningConstants.TimestampToleranceSeconds)
        {
            _logger.LogWarning("Timestamp expired: request={Timestamp}, now={Now}, diff={Diff}s",
                timestamp, now, Math.Abs(now - timestamp));
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "UNAUTHORIZED", message = "Invalid request signature." });
            return;
        }

        // リクエストボディ読み取り（EnableBuffering で再読み取り可能にする）
        context.Request.EnableBuffering();
        var bodyBytes = await ReadBodyAsync(context.Request);
        context.Request.Body.Position = 0;

        // ユーザーごとの鍵を導出
        var userKey = DeriveUserKey(userId);

        // HMAC 署名検証
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "/";
        var canonicalString = HmacRequestSigner.BuildCanonicalString(method, path, timestamp, nonce!, bodyBytes);

        if (!HmacRequestSigner.VerifySignature(userKey, canonicalString, signature!))
        {
            _logger.LogWarning("HMAC signature verification failed for {Method} {Path}", method, path);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "UNAUTHORIZED", message = "Invalid request signature." });
            return;
        }

        // Nonce 重複チェック（Valkey）
        var nonceResult = await TryAcceptNonce(context, nonce!);
        if (nonceResult == NonceResult.Unavailable)
        {
            _logger.LogWarning("Nonce validation unavailable (Valkey down)");
            context.Response.StatusCode = 503;
            await context.Response.WriteAsJsonAsync(new { error = "SERVICE_UNAVAILABLE", message = "Service temporarily unavailable." });
            return;
        }

        if (nonceResult == NonceResult.Replayed)
        {
            _logger.LogWarning("Nonce replay detected: {Nonce}", nonce.ToString());
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "UNAUTHORIZED", message = "Invalid request signature." });
            return;
        }

        await _next(context);
    }

    private byte[] DeriveUserKey(string userId)
    {
        var userIdBytes = Encoding.UTF8.GetBytes(userId);
        using var hmac = new HMACSHA256(_serverSecret);
        return hmac.ComputeHash(userIdBytes);
    }

    private static bool RequiresSignatureVerification(HttpRequest request)
    {
        // 書き込み系メソッドのみ検証（GET はスキップ）
        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsOptions(request.Method) || HttpMethods.IsHead(request.Method))
        {
            return false;
        }

        // /api/ 配下のエンドポイントのみ対象
        if (!request.Path.StartsWithSegments("/api"))
        {
            return false;
        }

        // 認証不要エンドポイントは署名検証をスキップ
        var path = request.Path.Value ?? "";
        foreach (var exempt in ExemptPaths)
        {
            if (path.Equals(exempt, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static async Task<byte[]> ReadBodyAsync(HttpRequest request)
    {
        using var ms = new MemoryStream();
        await request.Body.CopyToAsync(ms);
        return ms.ToArray();
    }

    private enum NonceResult { Accepted, Replayed, Unavailable }

    private async Task<NonceResult> TryAcceptNonce(HttpContext context, string nonce)
    {
        try
        {
            var redis = context.RequestServices.GetService<IConnectionMultiplexer>();
            if (redis == null || !redis.IsConnected)
            {
                _logger.LogWarning("Valkey not available, rejecting request for nonce validation");
                return NonceResult.Unavailable;
            }

            var db = redis.GetDatabase();
            var key = $"nonce:{nonce}";
            var wasSet = await db.StringSetAsync(key, "1",
                TimeSpan.FromSeconds(RequestSigningConstants.NonceExpirySeconds),
                When.NotExists);

            return wasSet ? NonceResult.Accepted : NonceResult.Replayed;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Valkey connection error during nonce check, rejecting request");
            return NonceResult.Unavailable;
        }
    }
}
