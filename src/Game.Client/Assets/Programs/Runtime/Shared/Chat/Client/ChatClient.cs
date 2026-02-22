using System;
using System.Linq;
using System.Threading.Tasks;
using Game.Library.Shared.Dto;
using Game.Shared.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace Game.Shared.Chat.Client
{
    /// <summary>
    /// チャットクライアント実装（REST + SignalR ハイブリッド）
    /// REST: ルーム管理操作（作成、削除、招待、キック、権限変更）
    /// SignalR: リアルタイム通信（参加、退出、メッセージ送信、イベント受信）
    /// 1接続で複数ルームに同時参加可能
    /// </summary>
    public class ChatClient : IChatClient
    {
        private readonly IApiClient _apiClient;
        private string _hubUrl;
        private Func<Task<string>> _accessTokenProvider;
        private HubConnection _hubConnection;
        private bool _disposed;

        public event Action<string, ChatMessage> OnMessageReceived;
        public event Action<string, string, string> OnPlayerJoined;
        public event Action<string, string, string> OnPlayerLeft;
        public event Action<string, string> OnRoomDeleted;
        public event Action<string, int> OnPermissionsChanged;

        public ChatClient(IApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        /// <summary>
        /// SignalR 接続に必要な情報を設定する
        /// ConnectAsync の前に呼び出すこと
        /// </summary>
        public void Configure(string hubUrl, Func<Task<string>> accessTokenProvider)
        {
            _hubUrl = hubUrl;
            _accessTokenProvider = accessTokenProvider;
        }

        // REST 操作

        public async Task<CreateChatRoomResponse> CreateRoomAsync(CreateChatRoomRequest request)
        {
            var response = await _apiClient.PostAsync<CreateChatRoomRequest, CreateChatRoomResponse>(
                "/api/chat/rooms", request);
            if (response.IsSuccess)
            {
                Debug.Log($"[ChatClient] Created chat room: {response.Data.RoomId}");
                return response.Data;
            }

            var errorMsg = response.Error?.Message ?? "Unknown error";
            Debug.LogError($"[ChatClient] Failed to create room: {errorMsg}");
            return new CreateChatRoomResponse { Success = false, ErrorMessage = errorMsg };
        }

        public async Task<bool> DeleteRoomAsync(string roomId)
        {
            var response = await _apiClient.DeleteAsync<SuccessResponse>(
                $"/api/chat/rooms/{roomId}");
            return response.IsSuccess && response.Data.Success;
        }

        public async Task<bool> InviteMemberAsync(string roomId, string targetUserId, string playerName)
        {
            var request = new InviteMemberRequest
            {
                TargetUserId = targetUserId,
                PlayerName = playerName,
            };
            var response = await _apiClient.PostAsync<InviteMemberRequest, SuccessResponse>(
                $"/api/chat/rooms/{roomId}/invite", request);
            return response.IsSuccess && response.Data.Success;
        }

        public async Task<bool> KickMemberAsync(string roomId, string targetUserId)
        {
            var request = new InviteMemberRequest
            {
                TargetUserId = targetUserId,
                PlayerName = string.Empty,
            };
            var response = await _apiClient.PostAsync<InviteMemberRequest, SuccessResponse>(
                $"/api/chat/rooms/{roomId}/kick", request);
            return response.IsSuccess && response.Data.Success;
        }

        public async Task<bool> SetMemberPermissionsAsync(string roomId, string targetUserId, int permissions)
        {
            var request = new SetPermissionsRequest { Permissions = permissions };
            var response = await _apiClient.PostAsync<SetPermissionsRequest, SuccessResponse>(
                $"/api/chat/rooms/{roomId}/members/{targetUserId}/permissions", request);
            return response.IsSuccess && response.Data.Success;
        }

        public async Task<ChatRoomInfo> GetRoomInfoAsync(string roomId)
        {
            var response = await _apiClient.GetAsync<ChatRoomInfo>(
                $"/api/chat/rooms/{roomId}");
            return response.IsSuccess ? response.Data : null;
        }

        public async Task<ChatRoomMemberInfo[]> GetRoomMembersAsync(string roomId)
        {
            var response = await _apiClient.GetAsync<ChatRoomMembersResponse>(
                $"/api/chat/rooms/{roomId}/members");
            return response.IsSuccess && response.Data?.Members != null
                ? response.Data.Members.ToArray()
                : Array.Empty<ChatRoomMemberInfo>();
        }

        // SignalR 操作

        public async Task ConnectAsync()
        {
            if (_hubConnection != null)
            {
                Debug.LogWarning("[ChatClient] Already connected to SignalR hub");
                return;
            }

            if (string.IsNullOrEmpty(_hubUrl) || _accessTokenProvider == null)
            {
                throw new InvalidOperationException(
                    "Hub URL and access token provider must be configured before connecting. Call Configure() first.");
            }

            _hubConnection = new HubConnectionBuilder()
                .WithUrl($"{_hubUrl}/hubs/chat", options =>
                {
                    options.AccessTokenProvider = _accessTokenProvider;
                })
                .AddMessagePackProtocol()
                .WithAutomaticReconnect()
                .Build();

            RegisterCallbacks();

            await _hubConnection.StartAsync();
            Debug.Log("[ChatClient] Connected to SignalR chat hub");
        }

        public async Task JoinAsync(string roomId, string playerName)
        {
            EnsureConnected();
            await _hubConnection.InvokeAsync("JoinAsync", roomId, playerName);
            Debug.Log($"[ChatClient] Joined chat room: {roomId}");
        }

        public async Task LeaveAsync(string roomId)
        {
            EnsureConnected();
            await _hubConnection.InvokeAsync("LeaveAsync", roomId);
            Debug.Log($"[ChatClient] Left chat room: {roomId}");
        }

        public async Task SendMessageAsync(string roomId, string content)
        {
            EnsureConnected();
            await _hubConnection.InvokeAsync("SendMessageAsync", roomId, content);
        }

        public async Task<ChatMessage[]> GetRecentMessagesAsync(string roomId, int count)
        {
            EnsureConnected();
            return await _hubConnection.InvokeAsync<ChatMessage[]>(
                "GetRecentMessagesAsync", roomId, count);
        }

        public async Task DisconnectAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                if (_hubConnection != null)
                {
                    try
                    {
                        await _hubConnection.StopAsync();
                        await _hubConnection.DisposeAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ChatClient] Disconnect error: {ex.Message}");
                    }
                    _hubConnection = null;
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                if (_hubConnection != null)
                {
                    try { _hubConnection.StopAsync().GetAwaiter().GetResult(); }
                    catch (Exception ex) { Debug.LogWarning($"[ChatClient] Dispose Stop error: {ex.Message}"); }
                    try { _hubConnection.DisposeAsync().GetAwaiter().GetResult(); }
                    catch (Exception ex) { Debug.LogWarning($"[ChatClient] Dispose error: {ex.Message}"); }
                    _hubConnection = null;
                }
            }
        }

        private void RegisterCallbacks()
        {
            _hubConnection.On<string, ChatMessage>("OnMessageReceived",
                (roomId, message) => OnMessageReceived?.Invoke(roomId, message));

            _hubConnection.On<string, string, string>("OnPlayerJoined",
                (roomId, userId, playerName) =>
                {
                    Debug.Log($"[ChatClient] Player joined {roomId}: {playerName} ({userId})");
                    OnPlayerJoined?.Invoke(roomId, userId, playerName);
                });

            _hubConnection.On<string, string, string>("OnPlayerLeft",
                (roomId, userId, playerName) =>
                {
                    Debug.Log($"[ChatClient] Player left {roomId}: {playerName} ({userId})");
                    OnPlayerLeft?.Invoke(roomId, userId, playerName);
                });

            _hubConnection.On<string, string>("OnRoomDeleted",
                (roomId, reason) =>
                {
                    Debug.Log($"[ChatClient] Room {roomId} deleted: {reason}");
                    OnRoomDeleted?.Invoke(roomId, reason);
                });

            _hubConnection.On<string, int>("OnPermissionsChanged",
                (roomId, permissions) =>
                {
                    Debug.Log($"[ChatClient] Permissions changed in {roomId}: {permissions}");
                    OnPermissionsChanged?.Invoke(roomId, permissions);
                });
        }

        private void EnsureConnected()
        {
            if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected)
            {
                throw new InvalidOperationException("SignalR hub is not connected. Call ConnectAsync first.");
            }
        }
    }
}
