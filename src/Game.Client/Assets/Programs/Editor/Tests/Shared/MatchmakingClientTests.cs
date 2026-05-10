using System.Threading.Tasks;
using Game.Shared.Realtime.Client;
using Game.Shared.Services;
using NSubstitute;
using NUnit.Framework;

namespace Game.Tests.Shared
{
    /// <summary>
    /// MatchmakingClient のテスト
    /// MagicOnion 静的メソッド (MagicOnionClient.Create, StreamingHubClient.ConnectAsync) に依存する
    /// メソッドは実サーバーが必要なためテスト対象外。状態管理ロジックのみ検証。
    /// </summary>
    [TestFixture]
    public class MatchmakingClientTests
    {
        private IGrpcChannelProvider _mockChannelProvider;
        private AuthClientFilter _authFilter;
        private MatchmakingClient _client;

        [SetUp]
        public void Setup()
        {
            _mockChannelProvider = Substitute.For<IGrpcChannelProvider>();
            var mockAuthSessionService = Substitute.For<IAuthSessionService>();
            mockAuthSessionService.AuthToken.Returns("test-token");
            _authFilter = new AuthClientFilter(mockAuthSessionService);
            _client = new MatchmakingClient(_mockChannelProvider, _authFilter);
        }

        [TearDown]
        public void TearDown()
        {
            _client?.Dispose();
        }

        #region Initial State

        [Test]
        public void IsSearching_IsFalse_Initially()
        {
            Assert.That(_client.IsSearching, Is.False);
        }

        #endregion

        #region CancelMatchmakingAsync

        [Test]
        public async Task CancelMatchmakingAsync_DoesNothing_WhenNotSearching()
        {
            // Act: IsSearching == false の状態でキャンセル → 例外なしで即 return
            await _client.CancelMatchmakingAsync();

            // Assert: 状態は変わらない
            Assert.That(_client.IsSearching, Is.False);
        }

        #endregion

        #region Dispose

        [Test]
        public void Dispose_DoesNotThrow_WhenNotConnected()
        {
            Assert.DoesNotThrow(() => _client.Dispose());
        }

        [Test]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            Assert.DoesNotThrow(() =>
            {
                _client.Dispose();
                _client.Dispose();
            });
        }

        [Test]
        public void IsSearching_IsFalse_AfterDispose()
        {
            _client.Dispose();
            Assert.That(_client.IsSearching, Is.False);
        }

        #endregion

        #region Events

        [Test]
        public void Events_CanBeSubscribedAndUnsubscribed()
        {
            // Arrange
            var matchFoundCalled = false;
            var queueUpdatedCalled = false;
            var cancelledCalled = false;
            var disconnectedCalled = false;

            void OnMatchFound(Game.Library.Shared.Dto.GameSessionStartInfo _) => matchFoundCalled = true;
            void OnQueueUpdated(int _) => queueUpdatedCalled = true;
            void OnCancelled(string _) => cancelledCalled = true;
            void OnDisconnected(string _) => disconnectedCalled = true;

            // Act: subscribe と unsubscribe が例外なく動作する
            Assert.DoesNotThrow(() =>
            {
                _client.OnMatchFound += OnMatchFound;
                _client.OnQueueStatusUpdated += OnQueueUpdated;
                _client.OnMatchmakingCancelled += OnCancelled;
                _client.OnDisconnected += OnDisconnected;

                _client.OnMatchFound -= OnMatchFound;
                _client.OnQueueStatusUpdated -= OnQueueUpdated;
                _client.OnMatchmakingCancelled -= OnCancelled;
                _client.OnDisconnected -= OnDisconnected;
            });

            Assert.That(matchFoundCalled, Is.False);
            Assert.That(queueUpdatedCalled, Is.False);
            Assert.That(cancelledCalled, Is.False);
            Assert.That(disconnectedCalled, Is.False);
        }

        #endregion
    }
}
