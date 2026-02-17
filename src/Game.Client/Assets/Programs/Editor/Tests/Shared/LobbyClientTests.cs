using System.Threading.Tasks;
using Game.Shared.Realtime.Client;
using Game.Shared.Services;
using NSubstitute;
using NUnit.Framework;

namespace Game.Tests.Shared
{
    /// <summary>
    /// LobbyClient のテスト
    /// MagicOnion 静的メソッドに依存するメソッドは実サーバーが必要なためテスト対象外。
    /// 状態管理と Hub 未接続時の安全な動作を検証。
    /// </summary>
    [TestFixture]
    public class LobbyClientTests
    {
        private IGrpcChannelProvider _mockChannelProvider;
        private AuthClientFilter _authFilter;
        private LobbyClient _client;

        [SetUp]
        public void Setup()
        {
            _mockChannelProvider = Substitute.For<IGrpcChannelProvider>();
            var mockAuthSessionService = Substitute.For<IAuthSessionService>();
            mockAuthSessionService.AuthToken.Returns("test-token");
            _authFilter = new AuthClientFilter(mockAuthSessionService);
            _client = new LobbyClient(_mockChannelProvider, _authFilter);
        }

        [TearDown]
        public void TearDown()
        {
            _client?.Dispose();
        }

        #region Initial State

        [Test]
        public void IsConnected_IsFalse_Initially()
        {
            Assert.That(_client.IsConnected, Is.False);
        }

        #endregion

        #region Hub Not Connected - Safe Behavior

        [Test]
        public async Task SendMessageAsync_DoesNothing_WhenHubNotConnected()
        {
            await _client.SendMessageAsync("Hello");
            Assert.Pass();
        }

        [Test]
        public async Task SetReadyAsync_DoesNothing_WhenHubNotConnected()
        {
            await _client.SetReadyAsync(true);
            Assert.Pass();
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
        public void IsConnected_IsFalse_AfterDispose()
        {
            _client.Dispose();
            Assert.That(_client.IsConnected, Is.False);
        }

        #endregion

        #region Events

        [Test]
        public void Events_CanBeSubscribedAndUnsubscribed()
        {
            var playerJoinedCalled = false;
            var playerLeftCalled = false;
            var messageReceivedCalled = false;
            var readyChangedCalled = false;
            var gameStartingCalled = false;
            var disconnectedCalled = false;

            void OnPlayerJoined(string a, string b) => playerJoinedCalled = true;
            void OnPlayerLeft(string a, string b) => playerLeftCalled = true;
            void OnMessageReceived(string a, string b, string c) => messageReceivedCalled = true;
            void OnReadyChanged(string a, bool b) => readyChangedCalled = true;
            void OnGameStarting(string a, string b, int c) => gameStartingCalled = true;
            void OnDisconnected(string _) => disconnectedCalled = true;

            Assert.DoesNotThrow(() =>
            {
                _client.OnPlayerJoined += OnPlayerJoined;
                _client.OnPlayerLeft += OnPlayerLeft;
                _client.OnMessageReceived += OnMessageReceived;
                _client.OnPlayerReadyChanged += OnReadyChanged;
                _client.OnGameStarting += OnGameStarting;
                _client.OnDisconnected += OnDisconnected;

                _client.OnPlayerJoined -= OnPlayerJoined;
                _client.OnPlayerLeft -= OnPlayerLeft;
                _client.OnMessageReceived -= OnMessageReceived;
                _client.OnPlayerReadyChanged -= OnReadyChanged;
                _client.OnGameStarting -= OnGameStarting;
                _client.OnDisconnected -= OnDisconnected;
            });

            Assert.That(playerJoinedCalled, Is.False);
            Assert.That(playerLeftCalled, Is.False);
            Assert.That(messageReceivedCalled, Is.False);
            Assert.That(readyChangedCalled, Is.False);
            Assert.That(gameStartingCalled, Is.False);
            Assert.That(disconnectedCalled, Is.False);
        }

        #endregion
    }
}
