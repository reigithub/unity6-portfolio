using Game.Server.Middleware;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Game.Server.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>
    /// 全 Controller endpoint の署名ポリシー属性宣言を検証する。
    /// 未宣言 or conflict があれば startup で fail-fast する。
    /// <see cref="IEndpointRouteBuilder.MapControllers"/> 等の endpoint registration 直後に呼び出す。
    /// </summary>
    /// <param name="app">Endpoint を収集済みの <see cref="IEndpointRouteBuilder"/>。</param>
    /// <returns>fluent chain 用に元の <paramref name="app"/> を返す。</returns>
    /// <exception cref="InvalidOperationException">
    /// Policy 属性未宣言 or 複数宣言の endpoint がある場合。
    /// </exception>
    public static IEndpointRouteBuilder ValidateRequestSigningPolicy(this IEndpointRouteBuilder app)
    {
        var dataSource = app.ServiceProvider.GetRequiredService<EndpointDataSource>();
        RequestSigningPolicyValidator.Validate(dataSource);
        return app;
    }
}
