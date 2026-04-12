using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.Dto;
using Game.Shared.Services;
using Game.Shared.Services.Network.Models;
using NSubstitute;
using NUnit.Framework;

namespace Game.Tests.Shared.Services
{
    /// <summary>
    /// <see cref="AuthApiService"/> の regression test。
    /// 主な目的は、本計画 [D] の key assumption「refresh 成功時に AuthApiService.OnLoginSuccessAsync が
    /// 自動で <see cref="IApiClient.SetAuthToken"/> + <see cref="IApiClient.SetSigningKey"/> を呼ぶ」
    /// を future の refactoring から守ること。
    /// </summary>
    [TestFixture]
    public class AuthApiServiceTests
    {
        private IApiClient _mockApiClient;
        private IAuthSessionService _mockSession;
        private AuthApiService _service;

        [SetUp]
        public void Setup()
        {
            _mockApiClient = Substitute.For<IApiClient>();
            _mockSession = Substitute.For<IAuthSessionService>();
            _service = new AuthApiService(_mockApiClient, _mockSession);
        }

        /// <summary>
        /// Refresh 成功時に新 JWT (Token) と新 signing key が IApiClient に設定されることを検証する。
        ///
        /// 本 test が pass することで、<see cref="Game.MVP.Survivor.SurvivorGameRunner"/>.StartupAsync が
        /// 期限切れ JWT を事前に SetAuthToken する冗長な step を削除しても、refresh 成功後に
        /// 正しく新 token が設定される不変条件が守られる。
        /// </summary>
        [Test]
        public async Task RefreshTokenAsync_OnSuccess_InvokesSetAuthTokenAndSetSigningKey()
        {
            // Arrange: refresh token が存在する state にする
            _mockSession.RefreshToken.Returns("valid-refresh-token");
            _mockSession.AuthType.Returns("guest");

            var loginResponse = new LoginResponse
            {
                UserId = "test-user-id",
                UserName = "TestUser",
                Token = "new-jwt-access-token",
                RefreshToken = "new-refresh-token",
                SigningKey = "new-signing-key-base64",
            };

            _mockApiClient.PostAsync<RefreshTokenRequest, LoginResponse>(
                    Arg.Any<string>(),
                    Arg.Any<RefreshTokenRequest>(),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<LoginResponse>
                {
                    IsSuccess = true,
                    Data = loginResponse,
                    StatusCode = 200,
                }));

            // Act
            var result = await _service.RefreshTokenAsync();

            // Assert: refresh 成功
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual("new-jwt-access-token", result.Data.Token);

            // Assert: IApiClient に新 token が設定された (OnLoginSuccessAsync の副作用)
            _mockApiClient.Received(1).SetAuthToken("new-jwt-access-token");
            _mockApiClient.Received(1).SetSigningKey("new-signing-key-base64");

            // Assert: session が persistent data に保存された
            await _mockSession.Received(1).SaveSessionAsync(
                Arg.Is<LoginResponse>(r => r.Token == "new-jwt-access-token"),
                "guest");

            // [D-Phase 1.5] Assert: LastRefreshedAt 更新のため MarkRefreshed が呼ばれる
            _mockSession.Received(1).MarkRefreshed();
        }

        /// <summary>
        /// Refresh token が未保存の場合、API を呼ばず即座に失敗 response を返すことを検証する。
        /// </summary>
        [Test]
        public async Task RefreshTokenAsync_WithoutRefreshToken_ReturnsFailureWithoutApiCall()
        {
            // Arrange: refresh token が無い state
            _mockSession.RefreshToken.Returns((string)null);

            // Act
            var result = await _service.RefreshTokenAsync();

            // Assert: 即座に失敗
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(401, result.StatusCode);

            // Assert: API は呼ばれていない
            await _mockApiClient.DidNotReceive().PostAsync<RefreshTokenRequest, LoginResponse>(
                Arg.Any<string>(),
                Arg.Any<RefreshTokenRequest>(),
                Arg.Any<RequestOptions>(),
                Arg.Any<CancellationToken>());

            // Assert: Token 設定も呼ばれていない
            _mockApiClient.DidNotReceive().SetAuthToken(Arg.Any<string>());
            _mockApiClient.DidNotReceive().SetSigningKey(Arg.Any<string>());
        }
    }
}
