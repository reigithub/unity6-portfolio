using System.Text;
using Game.Server.Attributes;
using Microsoft.AspNetCore.Routing;

namespace Game.Server.Middleware;

/// <summary>
/// 全 Controller endpoint が署名ポリシー属性を宣言しているかを検証する。
/// 未指定 or 複数指定が見つかった場合は <see cref="InvalidOperationException"/> で fail-fast する。
///
/// Primary gate として startup 時に <see cref="Game.Server.Extensions.WebApplicationExtensions.ValidateRequestSigningPolicy"/>
/// から呼び出される。Test からも同じ純関数 API で呼び出せる。
/// </summary>
public static class RequestSigningPolicyValidator
{
    /// <summary>
    /// 純関数 API: <see cref="EndpointDataSource"/> を直接受け取って検証する。
    /// test と startup の両方から identical なロジックを呼べる。
    /// </summary>
    /// <param name="dataSource">検証対象の endpoint data source。</param>
    /// <exception cref="InvalidOperationException">
    /// 1 つ以上の state-changing /api endpoint が policy 属性を宣言していない、
    /// または複数の policy 属性を持つ場合にスローされる。
    /// </exception>
    public static void Validate(EndpointDataSource dataSource)
    {
        var missing = new List<string>();
        var conflicts = new List<string>();

        foreach (var endpoint in dataSource.Endpoints)
        {
            if (endpoint is not RouteEndpoint routeEndpoint)
                continue;

            // State-changing HTTP method 以外はスキップ (middleware 境界と一致)
            var httpMethods = routeEndpoint.Metadata
                .GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? Array.Empty<string>();
            if (!httpMethods.Any(RequestSigningPolicy.RequiresPolicy))
                continue;

            // /api/ 配下のみ対象
            if (!RequestSigningPolicy.IsApiRoutePattern(routeEndpoint.RoutePattern.RawText))
                continue;

            var hasSkip = routeEndpoint.Metadata.GetMetadata<SkipRequestSigningAttribute>() != null;
            var hasUser = routeEndpoint.Metadata.GetMetadata<UserSignatureAttribute>() != null;
            var hasDs = routeEndpoint.Metadata.GetMetadata<UnityServerSignatureAttribute>() != null;

            var policyCount = (hasSkip ? 1 : 0) + (hasUser ? 1 : 0) + (hasDs ? 1 : 0);
            var display = routeEndpoint.DisplayName ?? routeEndpoint.RoutePattern.RawText ?? "(unknown)";

            if (policyCount == 0)
            {
                missing.Add($"  - {display}");
            }
            else if (policyCount > 1)
            {
                var tags = new List<string>();
                if (hasSkip) tags.Add("SkipRequestSigning");
                if (hasUser) tags.Add("RequireUserSignature");
                if (hasDs) tags.Add("UnityServerSignature");
                conflicts.Add($"  - {display} has conflicting policies: {string.Join(", ", tags)}");
            }
        }

        if (missing.Count == 0 && conflicts.Count == 0)
            return;

        var sb = new StringBuilder();
        sb.AppendLine("Request signing policy validation failed.");

        if (missing.Count > 0)
        {
            sb.AppendLine($"{missing.Count} endpoint(s) are missing a signing policy attribute:");
            foreach (var m in missing)
                sb.AppendLine(m);
        }

        if (conflicts.Count > 0)
        {
            sb.AppendLine($"{conflicts.Count} endpoint(s) have conflicting policy attributes:");
            foreach (var c in conflicts)
                sb.AppendLine(c);
        }

        sb.AppendLine();
        sb.AppendLine("Every state-changing REST endpoint under /api must declare exactly one of:");
        sb.AppendLine("  - [SkipRequestSigning]    Public/anonymous endpoint (login, refresh, email flows)");
        sb.AppendLine("  - [RequireUserSignature]  Authenticated user endpoint (JWT userId + HMAC signature)");
        sb.AppendLine("  - [UnityServerSignature]  Dedicated Server endpoint (DS shared secret HMAC)");
        sb.AppendLine();
        sb.AppendLine("See src/Game.Server/Attributes/ for attribute definitions and usage guidelines.");

        throw new InvalidOperationException(sb.ToString());
    }
}
