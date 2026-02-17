using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.Chat.Dto;
using Game.Library.Shared.Dto;
using Game.Shared.Chat.Client;
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
            var expectedResponse = new CreateChatRoomResponse
            {
                Success = true,
                RoomId = "room-123",
            };
            _mockApiClient
                .PostAsync<CreateChatRoomRequest, CreateChatRoomResponse>(
                    Arg.Is("/api/chat/rooms"),
                    Arg.Any<CreateChatRoomRequest>(),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<CreateChatRoomResponse>
                {
                    IsSuccess = true,
                    Data = expectedResponse,
                }));

            var request = new CreateChatRoomRequest { RoomName = "Test Room" };

            // Act
            var result = await _client.CreateRoomAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.RoomId, Is.EqualTo("room-123"));
        }

        [Test]
        public async Task CreateRoomAsync_ReturnsError_WhenApiFails()
        {
            // Arrange
            _mockApiClient
                .PostAsync<CreateChatRoomRequest, CreateChatRoomResponse>(
                    Arg.Any<string>(),
                    Arg.Any<CreateChatRoomRequest>(),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<CreateChatRoomResponse>
                {
                    IsSuccess = false,
                    Error = new ApiErrorResponse { Message = "Server error" },
                }));

            var request = new CreateChatRoomRequest { RoomName = "Test" };
            LogAssert.Expect(LogType.Error, "[ChatClient] Failed to create room: Server error");

            // Act
            var result = await _client.CreateRoomAsync(request);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("Server error"));
        }

        [Test]
        public async Task CreateRoomAsync_ReturnsUnknownError_WhenErrorIsNull()
        {
            // Arrange
            _mockApiClient
                .PostAsync<CreateChatRoomRequest, CreateChatRoomResponse>(
                    Arg.Any<string>(),
                    Arg.Any<CreateChatRoomRequest>(),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<CreateChatRoomResponse>
                {
                    IsSuccess = false,
                    Error = null,
                }));

            var request = new CreateChatRoomRequest();
            LogAssert.Expect(LogType.Error, "[ChatClient] Failed to create room: Unknown error");

            // Act
            var result = await _client.CreateRoomAsync(request);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("Unknown error"));
        }

        #endregion

        #region DeleteRoomAsync

        [Test]
        public async Task DeleteRoomAsync_ReturnsTrue_WhenSuccess()
        {
            // Arrange
            _mockApiClient
                .DeleteAsync<SuccessResponse>(
                    Arg.Is("/api/chat/rooms/room-1"),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<SuccessResponse>
                {
                    IsSuccess = true,
                    Data = new SuccessResponse { Success = true },
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
                .DeleteAsync<SuccessResponse>(
                    Arg.Any<string>(),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<SuccessResponse>
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
                .DeleteAsync<SuccessResponse>(
                    Arg.Any<string>(),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<SuccessResponse>
                {
                    IsSuccess = true,
                    Data = new SuccessResponse { Success = false },
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
                .PostAsync<InviteMemberRequest, SuccessResponse>(
                    Arg.Is("/api/chat/rooms/room-1/invite"),
                    Arg.Any<InviteMemberRequest>(),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<SuccessResponse>
                {
                    IsSuccess = true,
                    Data = new SuccessResponse { Success = true },
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
                .PostAsync<InviteMemberRequest, SuccessResponse>(
                    Arg.Any<string>(),
                    Arg.Any<InviteMemberRequest>(),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<SuccessResponse>
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
                .PostAsync<InviteMemberRequest, SuccessResponse>(
                    Arg.Is("/api/chat/rooms/room-1/kick"),
                    Arg.Any<InviteMemberRequest>(),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<SuccessResponse>
                {
                    IsSuccess = true,
                    Data = new SuccessResponse { Success = true },
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
                .PostAsync<InviteMemberRequest, SuccessResponse>(
                    Arg.Any<string>(),
                    Arg.Any<InviteMemberRequest>(),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<SuccessResponse>
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
                .PostAsync<SetPermissionsRequest, SuccessResponse>(
                    Arg.Is("/api/chat/rooms/room-1/members/user-2/permissions"),
                    Arg.Any<SetPermissionsRequest>(),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<SuccessResponse>
                {
                    IsSuccess = true,
                    Data = new SuccessResponse { Success = true },
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
                .PostAsync<SetPermissionsRequest, SuccessResponse>(
                    Arg.Any<string>(),
                    Arg.Any<SetPermissionsRequest>(),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<SuccessResponse>
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
            var expected = new ChatRoomInfo
            {
                RoomId = "room-1",
                RoomName = "Test Room",
                CurrentMembers = 3,
                MaxMembers = 10,
            };
            _mockApiClient
                .GetAsync<ChatRoomInfo>(
                    Arg.Is("/api/chat/rooms/room-1"),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<ChatRoomInfo>
                {
                    IsSuccess = true,
                    Data = expected,
                }));

            // Act
            var result = await _client.GetRoomInfoAsync("room-1");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.RoomId, Is.EqualTo("room-1"));
            Assert.That(result.RoomName, Is.EqualTo("Test Room"));
            Assert.That(result.CurrentMembers, Is.EqualTo(3));
        }

        [Test]
        public async Task GetRoomInfoAsync_ReturnsNull_WhenApiFails()
        {
            // Arrange
            _mockApiClient
                .GetAsync<ChatRoomInfo>(
                    Arg.Any<string>(),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<ChatRoomInfo>
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
            var membersResponse = new ChatRoomMembersResponse
            {
                Members = new[]
                {
                    new ChatRoomMemberInfo { UserId = "user-1", PlayerName = "Player1" },
                    new ChatRoomMemberInfo { UserId = "user-2", PlayerName = "Player2" },
                }
            };
            _mockApiClient
                .GetAsync<ChatRoomMembersResponse>(
                    Arg.Is("/api/chat/rooms/room-1/members"),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<ChatRoomMembersResponse>
                {
                    IsSuccess = true,
                    Data = membersResponse,
                }));

            // Act
            var result = await _client.GetRoomMembersAsync("room-1");

            // Assert
            Assert.That(result.Length, Is.EqualTo(2));
            Assert.That(result[0].UserId, Is.EqualTo("user-1"));
            Assert.That(result[1].PlayerName, Is.EqualTo("Player2"));
        }

        [Test]
        public async Task GetRoomMembersAsync_ReturnsEmpty_WhenApiFails()
        {
            // Arrange
            _mockApiClient
                .GetAsync<ChatRoomMembersResponse>(
                    Arg.Any<string>(),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<ChatRoomMembersResponse>
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
                .PostAsync<InviteMemberRequest, SuccessResponse>(
                    Arg.Any<string>(),
                    Arg.Do<InviteMemberRequest>(r => capturedRequest = r),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<SuccessResponse>
                {
                    IsSuccess = true,
                    Data = new SuccessResponse { Success = true },
                }));

            // Act
            await _client.InviteMemberAsync("room-1", "target-user", "TargetPlayer");

            // Assert: リクエストボディの中身を検証
            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest.TargetUserId, Is.EqualTo("target-user"));
            Assert.That(capturedRequest.PlayerName, Is.EqualTo("TargetPlayer"));
        }

        [Test]
        public async Task KickMemberAsync_SendsEmptyPlayerName()
        {
            // Arrange
            InviteMemberRequest capturedRequest = null;
            _mockApiClient
                .PostAsync<InviteMemberRequest, SuccessResponse>(
                    Arg.Any<string>(),
                    Arg.Do<InviteMemberRequest>(r => capturedRequest = r),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<SuccessResponse>
                {
                    IsSuccess = true,
                    Data = new SuccessResponse { Success = true },
                }));

            // Act
            await _client.KickMemberAsync("room-1", "target-user");

            // Assert: キック時は playerName が空文字
            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest.TargetUserId, Is.EqualTo("target-user"));
            Assert.That(capturedRequest.PlayerName, Is.EqualTo(string.Empty));
        }

        [Test]
        public async Task SetMemberPermissionsAsync_SendsCorrectPermissions()
        {
            // Arrange
            SetPermissionsRequest capturedRequest = null;
            _mockApiClient
                .PostAsync<SetPermissionsRequest, SuccessResponse>(
                    Arg.Any<string>(),
                    Arg.Do<SetPermissionsRequest>(r => capturedRequest = r),
                    Arg.Any<RequestOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new ApiResponse<SuccessResponse>
                {
                    IsSuccess = true,
                    Data = new SuccessResponse { Success = true },
                }));

            // Act
            await _client.SetMemberPermissionsAsync("room-1", "user-2", 15);

            // Assert
            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest.Permissions, Is.EqualTo(15));
        }

        #endregion
    }
}
