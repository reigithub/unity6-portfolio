using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Library.Shared.Realtime.Dto;
using Game.Library.Shared.Realtime.Hubs;
using Game.Library.Shared.Realtime.Services;
using MagicOnion.Client;
using UnityEngine;

namespace Game.Shared.Realtime.Client
{
    /// <summary>
    /// チャットクライアント実装（Unary + Hub ハイブリッド）
    /// Dictionary で roomId ごとに Hub 接続を管理し、複数ルーム同時参加に対応
    /// </summary>
    public class ChatClient : IChatClient
    {
        private readonly IGrpcChannelProvider _channelProvider;
        private readonly Dictionary<string, HubConnection> _hubs = new Dictionary<string, HubConnection>();
        private bool _disposed;

        public event Action<string, ChatMessage> OnMessageReceived;
        public event Action<string, string, string> OnPlayerJoined;
        public event Action<string, string, string> OnPlayerLeft;
        public event Action<string, string> OnRoomDeleted;
        public event Action<string, int> OnPermissionsChanged;

        public ChatClient(IGrpcChannelProvider channelProvider)
        {
            _channelProvider = channelProvider;
        }

        // Unary operations

        public async Task<CreateChatRoomResponse> CreateRoomAsync(CreateChatRoomRequest request)
        {
            var channel = _channelProvider.GetChannel();
            var service = MagicOnionClient.Create<IChatService>(channel);
            var response = await service.CreateRoomAsync(request);
            Debug.Log($"[ChatClient] Created chat room: {response.RoomId}");
            return response;
        }

        public async Task<bool> DeleteRoomAsync(string roomId)
        {
            var channel = _channelProvider.GetChannel();
            var service = MagicOnionClient.Create<IChatService>(channel);
            return await service.DeleteRoomAsync(roomId);
        }

        public async Task<bool> InviteMemberAsync(string roomId, string targetUserId, string playerName)
        {
            var channel = _channelProvider.GetChannel();
            var service = MagicOnionClient.Create<IChatService>(channel);
            return await service.InviteMemberAsync(roomId, targetUserId, playerName);
        }

        public async Task<bool> KickMemberAsync(string roomId, string targetUserId)
        {
            var channel = _channelProvider.GetChannel();
            var service = MagicOnionClient.Create<IChatService>(channel);
            return await service.KickMemberAsync(roomId, targetUserId);
        }

        public async Task<bool> SetMemberPermissionsAsync(string roomId, string targetUserId, int permissions)
        {
            var channel = _channelProvider.GetChannel();
            var service = MagicOnionClient.Create<IChatService>(channel);
            return await service.SetMemberPermissionsAsync(roomId, targetUserId, permissions);
        }

        public async Task<ChatRoomInfo> GetRoomInfoAsync(string roomId)
        {
            var channel = _channelProvider.GetChannel();
            var service = MagicOnionClient.Create<IChatService>(channel);
            return await service.GetRoomInfoAsync(roomId);
        }

        public async Task<ChatRoomMemberInfo[]> GetRoomMembersAsync(string roomId)
        {
            var channel = _channelProvider.GetChannel();
            var service = MagicOnionClient.Create<IChatService>(channel);
            return await service.GetRoomMembersAsync(roomId);
        }

        // Hub operations

        public async Task JoinAsync(string roomId, string playerName)
        {
            if (_hubs.ContainsKey(roomId))
            {
                Debug.LogWarning($"[ChatClient] Already joined room: {roomId}");
                return;
            }

            var channel = _channelProvider.GetChannel();
            var receiver = new HubReceiver(roomId, this);
            var hub = await StreamingHubClient.ConnectAsync<IChatHub, IChatHubReceiver>(
                channel, receiver);
            await hub.JoinAsync(roomId, playerName);

            _hubs[roomId] = new HubConnection(hub, receiver);
            Debug.Log($"[ChatClient] Joined chat room: {roomId}");
        }

        public async Task LeaveAsync(string roomId)
        {
            if (_hubs.TryGetValue(roomId, out var connection))
            {
                await connection.Hub.LeaveAsync();
                await connection.Hub.DisposeAsync();
                _hubs.Remove(roomId);
                Debug.Log($"[ChatClient] Left chat room: {roomId}");
            }
        }

        public async Task SendMessageAsync(string roomId, string content)
        {
            if (_hubs.TryGetValue(roomId, out var connection))
            {
                await connection.Hub.SendMessageAsync(content);
            }
        }

        public async Task<ChatMessage[]> GetRecentMessagesAsync(string roomId, int count)
        {
            if (_hubs.TryGetValue(roomId, out var connection))
            {
                return await connection.Hub.GetRecentMessagesAsync(count);
            }

            return Array.Empty<ChatMessage>();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                foreach (var connection in _hubs.Values)
                {
                    connection.Hub.DisposeAsync().GetAwaiter().GetResult();
                }
                _hubs.Clear();
            }
        }

        /// <summary>
        /// Hub 接続とレシーバーのペア
        /// </summary>
        private class HubConnection
        {
            public IChatHub Hub { get; }
            public HubReceiver Receiver { get; }

            public HubConnection(IChatHub hub, HubReceiver receiver)
            {
                Hub = hub;
                Receiver = receiver;
            }
        }

        /// <summary>
        /// roomId をタグ付けしてイベントを発火する IChatHubReceiver 実装
        /// </summary>
        private class HubReceiver : IChatHubReceiver
        {
            private readonly string _roomId;
            private readonly ChatClient _client;

            public HubReceiver(string roomId, ChatClient client)
            {
                _roomId = roomId;
                _client = client;
            }

            public void OnMessageReceived(ChatMessage message)
            {
                _client.OnMessageReceived?.Invoke(_roomId, message);
            }

            public void OnPlayerJoined(string userId, string playerName)
            {
                Debug.Log($"[ChatClient] Player joined {_roomId}: {playerName} ({userId})");
                _client.OnPlayerJoined?.Invoke(_roomId, userId, playerName);
            }

            public void OnPlayerLeft(string userId, string playerName)
            {
                Debug.Log($"[ChatClient] Player left {_roomId}: {playerName} ({userId})");
                _client.OnPlayerLeft?.Invoke(_roomId, userId, playerName);
            }

            public void OnRoomDeleted(string reason)
            {
                Debug.Log($"[ChatClient] Room {_roomId} deleted: {reason}");
                _client.OnRoomDeleted?.Invoke(_roomId, reason);
            }

            public void OnPermissionsChanged(int permissions)
            {
                Debug.Log($"[ChatClient] Permissions changed in {_roomId}: {permissions}");
                _client.OnPermissionsChanged?.Invoke(_roomId, permissions);
            }
        }
    }
}
