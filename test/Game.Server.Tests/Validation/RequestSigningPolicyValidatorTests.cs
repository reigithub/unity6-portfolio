using Game.Server.Middleware;
using Game.Server.Tests.Fixtures;
using Game.Server.Tests.Integration;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Game.Server.Tests.Validation;

/// <summary>
/// 全 Controller endpoint が署名ポリシー属性を宣言していることを CI レベルで守る regression test。
/// Startup validation (<see cref="Game.Server.Extensions.WebApplicationExtensions.ValidateRequestSigningPolicy"/>)
/// が CustomWebApplicationFactory 経由で既に実行されるため実質二重防御だが、
/// test 名として明示的に CI output に記録されることに価値がある。
/// </summary>
[Collection("Database")]
public class RequestSigningPolicyValidatorTests
{
    private readonly PostgresContainerFixture _postgres;

    public RequestSigningPolicyValidatorTests(PostgresContainerFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact]
    public void AllEndpoints_DeclareSigningPolicy()
    {
        // CustomWebApplicationFactory 経由で TestServer を起動 (DB 依存のため既存 fixture 共有)。
        // この factory 経由の Startup 時点で既に ValidateRequestSigningPolicy が走っているため、
        // factory 作成が成功すれば時点で「policy 宣言は正常」である。
        using var factory = new CustomWebApplicationFactory(_postgres.ConnectionString);

        // Services から EndpointDataSource を取得して純関数 Validate を呼ぶ。
        // Startup 時の app.ValidateRequestSigningPolicy() と identical なロジックを test で実行する。
        var dataSource = factory.Services.GetRequiredService<EndpointDataSource>();

        // 例外が投げられなければ全 endpoint が policy を宣言している
        RequestSigningPolicyValidator.Validate(dataSource);
    }
}
