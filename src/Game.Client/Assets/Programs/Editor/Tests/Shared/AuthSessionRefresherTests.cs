using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.Dto;
using Game.Shared.Bootstrap;
using Game.Shared.Services;
using Game.Shared.Services.Network;
using Game.Shared.Signals.Survivor;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using R3;

namespace Game.Tests.Shared.Services
{
    /// <summary>
    /// <see cref="AuthSessionRefresher"/> の unit test。
    /// Periodic loop / trigger subscribe の time-based test は困難なため、
    /// StartAsync を呼ばずに <see cref="IAuthSessionRefresher.EnsureFreshAsync"/> を
    /// 直接呼ぶ形で dedup / refresh flow を検証する。
    /// </summary>
    [TestFixture]
    public class AuthSessionRefresherTests
    {
        private IAuthSessionService _session;
        private IAuthApiService _authApi;
        private INetworkService _network;
        private IAppLifecycleSignals _lifecycle;
        private IPublisher<SurvivorSignals.Auth.SessionRefreshResult> _publisher;
        private AuthSessionRefresher _refresher;

        [SetUp]
        public void Setup()
        {
            _session = Substitute.For<IAuthSessionService>();
            _authApi = Substitute.For<IAuthApiService>();
            _network = Substitute.For<INetworkService>();
            _lifecycle = Substitute.For<IAppLifecycleSignals>();
            _publisher = Substitute.For<IPublisher<SurvivorSignals.Auth.SessionRefreshResult>>();

            // Trigger の Observable は Never を返して無効化
            _lifecycle.OnFocusChanged.Returns(Observable.Never<bool>());
            _network.OnConnectivityChanged.Returns(Observable.Never<bool>());

            _refresher = new AuthSessionRefresher(_session, _authApi, _network, _lifecycle, _publisher);
        }

        [Test]
        public async Task EnsureFreshAsync_NotAuthenticated_ReturnsFalse()
        {
            _session.IsAuthenticated.Returns(false);

            var result = await _refresher.EnsureFreshAsync();

            Assert.IsFalse(result);
            // API は呼ばれない
            await _authApi.DidNotReceive().RefreshTokenAsync();
        }

        [Test]
        public async Task EnsureFreshAsync_RecentlyRefreshed_SkipsRefreshAndReturnsTrue()
        {
            _session.IsAuthenticated.Returns(true);
            _session.IsRecentlyRefreshed().Returns(true);  // 直近 refresh 済み → skip

            var result = await _refresher.EnsureFreshAsync();

            Assert.IsTrue(result);
            await _authApi.DidNotReceive().RefreshTokenAsync();
        }

        [Test]
        public async Task EnsureFreshAsync_StaleSession_CallsRefreshTokenAsync()
        {
            _session.IsAuthenticated.Returns(true);
            _session.IsRecentlyRefreshed().Returns(false);  // stale

            _authApi.RefreshTokenAsync().Returns(UniTask.FromResult(new ApiResponse<LoginResponse>
            {
                IsSuccess = true,
                Data = new LoginResponse { Token = "new-token" },
            }));

            var result = await _refresher.EnsureFreshAsync();

            Assert.IsTrue(result);
            await _authApi.Received(1).RefreshTokenAsync();
            Assert.AreEqual(1, _refresher.TotalRefreshCount);
            Assert.AreEqual(0, _refresher.FailedRefreshCount);
            Assert.AreEqual(RefreshTrigger.Explicit, _refresher.LastRefreshTrigger);
        }

        [Test]
        public async Task EnsureFreshAsync_OnSuccess_PublishesResultEvent()
        {
            _session.IsAuthenticated.Returns(true);
            _session.IsRecentlyRefreshed().Returns(false);

            _authApi.RefreshTokenAsync().Returns(UniTask.FromResult(new ApiResponse<LoginResponse>
            {
                IsSuccess = true,
                Data = new LoginResponse { Token = "new-token" },
            }));

            await _refresher.EnsureFreshAsync();

            _publisher.Received(1).Publish(Arg.Is<SurvivorSignals.Auth.SessionRefreshResult>(
                r => r.IsSuccess && r.Trigger == RefreshTrigger.Explicit));
        }

        [Test]
        public async Task EnsureFreshAsync_OnFailure_IncrementsFailedCount()
        {
            _session.IsAuthenticated.Returns(true);
            _session.IsRecentlyRefreshed().Returns(false);

            _authApi.RefreshTokenAsync().Returns(UniTask.FromResult(new ApiResponse<LoginResponse>
            {
                IsSuccess = false,
                Error = new ApiErrorResponse { Message = "Invalid refresh token" },
                StatusCode = 401,
            }));

            var result = await _refresher.EnsureFreshAsync();

            Assert.IsFalse(result);
            Assert.AreEqual(1, _refresher.TotalRefreshCount);
            Assert.AreEqual(1, _refresher.FailedRefreshCount);
            _publisher.Received(1).Publish(Arg.Is<SurvivorSignals.Auth.SessionRefreshResult>(
                r => !r.IsSuccess && r.ErrorMessage == "Invalid refresh token"));
        }

        [Test]
        public async Task EnsureFreshAsync_ConcurrentCallers_DeduplicatedToSingleRefresh()
        {
            _session.IsAuthenticated.Returns(true);
            _session.IsRecentlyRefreshed().Returns(false);

            // Refresh に 100ms の遅延を入れて並列性をシミュレート
            var tcs = new UniTaskCompletionSource<ApiResponse<LoginResponse>>();
            _authApi.RefreshTokenAsync().Returns(tcs.Task);

            // 3 並列で EnsureFreshAsync を起動
            var t1 = _refresher.EnsureFreshAsync();
            var t2 = _refresher.EnsureFreshAsync();
            var t3 = _refresher.EnsureFreshAsync();

            // 全 caller が同じ in-flight を待つ
            tcs.TrySetResult(new ApiResponse<LoginResponse>
            {
                IsSuccess = true,
                Data = new LoginResponse { Token = "new-token" },
            });

            var results = await UniTask.WhenAll(t1, t2, t3);

            Assert.IsTrue(results.Item1);
            Assert.IsTrue(results.Item2);
            Assert.IsTrue(results.Item3);

            // API は 1 回しか呼ばれない (dedup)
            await _authApi.Received(1).RefreshTokenAsync();
            Assert.AreEqual(1, _refresher.TotalRefreshCount);
        }
    }
}
