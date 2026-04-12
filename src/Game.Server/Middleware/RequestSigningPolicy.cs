namespace Game.Server.Middleware;

/// <summary>
/// <see cref="RequestSigningMiddleware"/> と <see cref="RequestSigningPolicyValidator"/> が
/// 共有する境界条件の定義。両者が同じルールを参照することで drift を防ぐ。
/// </summary>
internal static class RequestSigningPolicy
{
    /// <summary>
    /// State-changing HTTP method の集合。これらのメソッドは署名ポリシー宣言を必須とする。
    /// GET/HEAD/OPTIONS は middleware で skip されるので要求対象外。
    /// </summary>
    public static readonly IReadOnlySet<string> StateChangingMethods =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            HttpMethods.Post,
            HttpMethods.Put,
            HttpMethods.Delete,
            HttpMethods.Patch,
        };

    /// <summary><see cref="HttpRequest.Path"/> 用 (leading slash あり)。</summary>
    public const string ApiPathPrefix = "/api";

    /// <summary><see cref="Microsoft.AspNetCore.Routing.Patterns.RoutePattern.RawText"/> 用 (leading slash なし)。</summary>
    public const string ApiRoutePatternPrefix = "api/";

    /// <summary>指定した HTTP method が署名ポリシー宣言を必要とするかを返す。</summary>
    public static bool RequiresPolicy(string httpMethod) =>
        StateChangingMethods.Contains(httpMethod);

    /// <summary>Request path が <c>/api</c> 配下かを判定する。</summary>
    public static bool IsApiPath(PathString path) =>
        path.StartsWithSegments(ApiPathPrefix);

    /// <summary>Route pattern raw text が <c>api/</c> 配下かを判定する。</summary>
    public static bool IsApiRoutePattern(string? rawText) =>
        rawText != null && rawText.StartsWith(ApiRoutePatternPrefix, StringComparison.Ordinal);
}
