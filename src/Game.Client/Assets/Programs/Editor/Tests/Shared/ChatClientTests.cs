using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.Chat.Dto;
using Game.Shared.Chat.Client;
using Game.Shared.Dto.Chat;
using Game.Shared.Services;
using Game.Shared.Services.Network.Models;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Shared
{
    /// <summary>
    /// ChatClient のテスト
    /// REST 操作（IApiClient mock）とバリデーションロジックを検証
    /// SignalR 接続は実サーバーが必要なためテスト対象外
    /// </summary>
    [TestFixture]
    public class ChatClientTests
    {
        private IApiClient _mockApiClient;
        private ChatClient _client;

        [SetUp]
        public void Setup()
        {
            _mockApiClient = Substitute.For<IApiClient>();
            _client = new ChatClient(_mockApiClient);
        }

        [TearDown]
        public void TearDown()
        {
            _client?.Dispose();
        }

        #region Constructor

        [Test]
        public void Constructor_WithValidApiClient_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => new ChatClient(_mockApiClient));
        }

        #endregion

        #region CreateRoomAsync

        [Test]
        public async Task CreateRoomAsync_ReturnsResponse_WhenSuccess()
        {
            // Arrange
            var expectedResponse = new CreateChatRoomRestResponse
            {
                success = true,
                roomId = "room-123",
            };
            _mockApiClient
                .PostAsync<CreateChatRoomRestRequest, CreateChatRoomRestResponse>(
                    Arg.Is("/api/chat/rooms"),
                    Arg.Any<CreateChatRoomRestRequest>(),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<CreateChatRoomRestResponse>
                {
                    IsSuccess = true,
                    Data = expectedResponse,
                }));

            var request = new CreateChatRoomRestRequest { roomName = "Test Room" };

            // Act
            var result = await _client.CreateRoomAsync(request);

            // Assert
            Assert.That(result.success, Is.True);
            Assert.That(result.roomId, Is.EqualTo("room-123"));
        }

        [Test]
        public async Task CreateRoomAsync_ReturnsError_WhenApiFails()
        {
            // Arrange
            _mockApiClient
                .PostAsync<CreateChatRoomRestRequest, CreateChatRoomRestResponse>(
                    Arg.Any<string>(),
                    Arg.Any<CreateChatRoomRestRequest>(),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<CreateChatRoomRestResponse>
                {
                    IsSuccess = false,
                    Error = new ApiErrorResponse { message = "Server error" },
                }));

            var request = new CreateChatRoomRestRequest { roomName = "Test" };
            LogAssert.Expect(LogType.Error, "[ChatClient] Failed to create room: Server error");

            // Act
            var result = await _client.CreateRoomAsync(request);

            // Assert
            Assert.That(result.success, Is.False);
            Assert.That(result.errorMessage, Is.EqualTo("Server error"));
        }

        [Test]
        public async Task CreateRoomAsync_ReturnsUnknownError_WhenErrorIsNull()
        {
            // Arrange
            _mockApiClient
                .PostAsync<CreateChatRoomRestRequest, CreateChatRoomRestResponse>(
                    Arg.Any<string>(),
                    Arg.Any<CreateChatRoomRestRequest>(),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<CreateChatRoomRestResponse>
                {
                    IsSuccess = false,
                    Error = null,
                }));

            var request = new CreateChatRoomRestRequest();
            LogAssert.Expect(LogType.Error, "[ChatClient] Failed to create room: Unknown error");

            // Act
            var result = await _client.CreateRoomAsync(request);

            // Assert
            Assert.That(result.success, Is.False);
            Assert.That(result.errorMessage, Is.EqualTo("Unknown error"));
        }

        #endregion

        #region DeleteRoomAsync

        [Test]
        public async Task DeleteRoomAsync_ReturnsTrue_WhenSuccess()
        {
            // Arrange
            _mockApiClient
                .DeleteAsync<ChatOperationResponse>(
                    Arg.Is("/api/chat/rooms/room-1"),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<ChatOperationResponse>
                {
                    IsSuccess = true,
                    Data = new ChatOperationResponse { success = true },
                }));

            // Act
            var result = await _client.DeleteRoomAsync("room-1");

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task DeleteRoomAsync_ReturnsFalse_WhenApiFails()
        {
            // Arrange
            _mockApiClient
                .DeleteAsync<ChatOperationResponse>(
                    Arg.Any<string>(),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<ChatOperationResponse>
                {
                    IsSuccess = false,
                }));

            // Act
            var result = await _client.DeleteRoomAsync("room-1");

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task DeleteRoomAsync_ReturnsFalse_WhenServerReturnsFailure()
        {
            // Arrange
            _mockApiClient
                .DeleteAsync<ChatOperationResponse>(
                    Arg.Any<string>(),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<ChatOperationResponse>
                {
                    IsSuccess = true,
                    Data = new ChatOperationResponse { success = false },
                }));

            // Act
            var result = await _client.DeleteRoomAsync("room-1");

            // Assert
            Assert.That(result, Is.False);
        }

        #endregion

        #region InviteMemberAsync

        [Test]
        public async Task InviteMemberAsync_ReturnsTrue_WhenSuccess()
        {
            // Arrange
            _mockApiClient
                .PostAsync<InviteMemberRequest, ChatOperationResponse>(
                    Arg.Is("/api/chat/rooms/room-1/invite"),
                    Arg.Any<InviteMemberRequest>(),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<ChatOperationResponse>
                {
                    IsSuccess = true,
                    Data = new ChatOperationResponse { success = true },
                }));

            // Act
            var result = await _client.InviteMemberAsync("room-1", "user-2", "Player2");

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task InviteMemberAsync_ReturnsFalse_WhenApiFails()
        {
            // Arrange
            _mockApiClient
                .PostAsync<InviteMemberRequest, ChatOperationResponse>(
                    Arg.Any<string>(),
                    Arg.Any<InviteMemberRequest>(),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<ChatOperationResponse>
                {
                    IsSuccess = false,
                }));

            // Act
            var result = await _client.InviteMemberAsync("room-1", "user-2", "Player2");

            // Assert
            Assert.That(result, Is.False);
        }

        #endregion

        #region KickMemberAsync

        [Test]
        public async Task KickMemberAsync_ReturnsTrue_WhenSuccess()
        {
            // Arrange
            _mockApiClient
                .PostAsync<InviteMemberRequest, ChatOperationResponse>(
                    Arg.Is("/api/chat/rooms/room-1/kick"),
                    Arg.Any<InviteMemberRequest>(),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<ChatOperationResponse>
                {
                    IsSuccess = true,
                    Data = new ChatOperationResponse { success = true },
                }));

            // Act
            var result = await _client.KickMemberAsync("room-1", "user-2");

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task KickMemberAsync_ReturnsFalse_WhenApiFails()
        {
            // Arrange
            _mockApiClient
                .PostAsync<InviteMemberRequest, ChatOperationResponse>(
                    Arg.Any<string>(),
                    Arg.Any<InviteMemberRequest>(),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<ChatOperationResponse>
                {
                    IsSuccess = false,
                }));

            // Act
            var result = await _client.KickMemberAsync("room-1", "user-2");

            // Assert
            Assert.That(result, Is.False);
        }

        #endregion

        #region SetMemberPermissionsAsync

        [Test]
        public async Task SetMemberPermissionsAsync_ReturnsTrue_WhenSuccess()
        {
            // Arrange
            _mockApiClient
                .PostAsync<SetPermissionsRequest, ChatOperationResponse>(
                    Arg.Is("/api/chat/rooms/room-1/members/user-2/permissions"),
                    Arg.Any<SetPermissionsRequest>(),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<ChatOperationResponse>
                {
                    IsSuccess = true,
                    Data = new ChatOperationResponse { success = true },
                }));

            // Act
            var result = await _client.SetMemberPermissionsAsync("room-1", "user-2", 7);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task SetMemberPermissionsAsync_ReturnsFalse_WhenApiFails()
        {
            // Arrange
            _mockApiClient
                .PostAsync<SetPermissionsRequest, ChatOperationResponse>(
                    Arg.Any<string>(),
                    Arg.Any<SetPermissionsRequest>(),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<ChatOperationResponse>
                {
                    IsSuccess = false,
                }));

            // Act
            var result = await _client.SetMemberPermissionsAsync("room-1", "user-2", 7);

            // Assert
            Assert.That(result, Is.False);
        }

        #endregion

        #region GetRoomInfoAsync

        [Test]
        public async Task GetRoomInfoAsync_ReturnsData_WhenSuccess()
        {
            // Arrange
            var expected = new ChatRoomInfoResponse
            {
                roomId = "room-1",
                roomName = "Test Room",
                currentMembers = 3,
                maxMembers = 10,
            };
            _mockApiClient
                .GetAsync<ChatRoomInfoResponse>(
                    Arg.Is("/api/chat/rooms/room-1"),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<ChatRoomInfoResponse>
                {
                    IsSuccess = true,
                    Data = expected,
                }));

            // Act
            var result = await _client.GetRoomInfoAsync("room-1");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.roomId, Is.EqualTo("room-1"));
            Assert.That(result.roomName, Is.EqualTo("Test Room"));
            Assert.That(result.currentMembers, Is.EqualTo(3));
        }

        [Test]
        public async Task GetRoomInfoAsync_ReturnsNull_WhenApiFails()
        {
            // Arrange
            _mockApiClient
                .GetAsync<ChatRoomInfoResponse>(
                    Arg.Any<string>(),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<ChatRoomInfoResponse>
                {
                    IsSuccess = false,
                }));

            // Act
            var result = await _client.GetRoomInfoAsync("room-1");

            // Assert
            Assert.That(result, Is.Null);
        }

        #endregion

        #region GetRoomMembersAsync

        [Test]
        public async Task GetRoomMembersAsync_ReturnsMembers_WhenSuccess()
        {
            // Arrange
            var members = new[]
            {
                new ChatRoomMemberInfoResponse { userId = "user-1", playerName = "Player1" },
                new ChatRoomMemberInfoResponse { userId = "user-2", playerName = "Player2" },
            };
            _mockApiClient
                .GetAsync<ChatRoomMemberInfoResponse[]>(
                    Arg.Is("/api/chat/rooms/room-1/members"),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<ChatRoomMemberInfoResponse[]>
                {
                    IsSuccess = true,
                    Data = members,
                }));

            // Act
            var result = await _client.GetRoomMembersAsync("room-1");

            // Assert
            Assert.That(result.Length, Is.EqualTo(2));
            Assert.That(result[0].userId, Is.EqualTo("user-1"));
            Assert.That(result[1].playerName, Is.EqualTo("Player2"));
        }

        [Test]
        public async Task GetRoomMembersAsync_ReturnsEmpty_WhenApiFails()
        {
            // Arrange
            _mockApiClient
                .GetAsync<ChatRoomMemberInfoResponse[]>(
                    Arg.Any<string>(),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<ChatRoomMemberInfoResponse[]>
                {
                    IsSuccess = false,
                }));

            // Act
            var result = await _client.GetRoomMembersAsync("room-1");

            // Assert
            Assert.That(result, Is.Empty);
        }

        #endregion

        #region ConnectAsync - Validation

        [Test]
        public void ConnectAsync_ThrowsInvalidOperationException_WhenNotConfigured()
        {
            // Act & Assert: Configure() を呼ばずに ConnectAsync
            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _client.ConnectAsync());

            Assert.That(ex.Message, Does.Contain("Configure"));
        }

        [Test]
        public void ConnectAsync_ThrowsInvalidOperationException_WhenHubUrlEmpty()
        {
            // Arrange
            _client.Configure("", () => Task.FromResult("token"));

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _client.ConnectAsync());
        }

        [Test]
        public void ConnectAsync_ThrowsInvalidOperationException_WhenTokenProviderNull()
        {
            // Arrange
            _client.Configure("http://localhost:5000", null);

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _client.ConnectAsync());
        }

        #endregion

        #region SignalR Methods - EnsureConnected Validation

        [Test]
        public void JoinAsync_ThrowsInvalidOperationException_WhenNotConnected()
        {
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _client.JoinAsync("room-1", "Player1"));
        }

        [Test]
        public void LeaveAsync_ThrowsInvalidOperationException_WhenNotConnected()
        {
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _client.LeaveAsync("room-1"));
        }

        [Test]
        public void SendMessageAsync_ThrowsInvalidOperationException_WhenNotConnected()
        {
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _client.SendMessageAsync("room-1", "Hello"));
        }

        [Test]
        public void GetRecentMessagesAsync_ThrowsInvalidOperationException_WhenNotConnected()
        {
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _client.GetRecentMessagesAsync("room-1", 10));
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
            // Act & Assert: 二重 Dispose しても例外なし
            Assert.DoesNotThrow(() =>
            {
                _client.Dispose();
                _client.Dispose();
            });
        }

        #endregion

        #region API Path Verification

        [Test]
        public async Task InviteMemberAsync_SendsCorrectRequestBody()
        {
            // Arrange
            InviteMemberRequest capturedRequest = null;
            _mockApiClient
                .PostAsync<InviteMemberRequest, ChatOperationResponse>(
                    Arg.Any<string>(),
                    Arg.Do<InviteMemberRequest>(r => capturedRequest = r),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<ChatOperationResponse>
                {
                    IsSuccess = true,
                    Data = new ChatOperationResponse { success = true },
                }));

            // Act
            await _client.InviteMemberAsync("room-1", "target-user", "TargetPlayer");

            // Assert: リクエストボディの中身を検証
            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest.targetUserId, Is.EqualTo("target-user"));
            Assert.That(capturedRequest.playerName, Is.EqualTo("TargetPlayer"));
        }

        [Test]
        public async Task KickMemberAsync_SendsEmptyPlayerName()
        {
            // Arrange
            InviteMemberRequest capturedRequest = null;
            _mockApiClient
                .PostAsync<InviteMemberRequest, ChatOperationResponse>(
                    Arg.Any<string>(),
                    Arg.Do<InviteMemberRequest>(r => capturedRequest = r),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<ChatOperationResponse>
                {
                    IsSuccess = true,
                    Data = new ChatOperationResponse { success = true },
                }));

            // Act
            await _client.KickMemberAsync("room-1", "target-user");

            // Assert: キック時は playerName が空文字
            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest.targetUserId, Is.EqualTo("target-user"));
            Assert.That(capturedRequest.playerName, Is.EqualTo(string.Empty));
        }

        [Test]
        public async Task SetMemberPermissionsAsync_SendsCorrectPermissions()
        {
            // Arrange
            SetPermissionsRequest capturedRequest = null;
            _mockApiClient
                .PostAsync<SetPermissionsRequest, ChatOperationResponse>(
                    Arg.Any<string>(),
                    Arg.Do<SetPermissionsRequest>(r => capturedRequest = r),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<ChatOperationResponse>
                {
                    IsSuccess = true,
                    Data = new ChatOperationResponse { success = true },
                }));

            // Act
            await _client.SetMemberPermissionsAsync("room-1", "user-2", 15);

            // Assert
            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest.permissions, Is.EqualTo(15));
        }

        #endregion
    }
}
