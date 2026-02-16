using System;
using System.Threading.Tasks;
using Game.Library.Shared.Realtime.Hubs;
using MagicOnion.Client;
using UnityEngine;

namespace Game.Shared.Realtime.Client
{
    /// <summary>
    /// チャットクライアント実装（Hub ベース）
    /// </summary>
    public class ChatClient : IChatClient, IChatHubReceiver
    {
        private readonly IGrpcChannelProvider _channelProvider;

        private IChatHub _hub;
        private bool _disposed;

        public bool IsConnected => _hub != null && !_disposed;

        public event Action<ChatMessage> OnMessageReceived;
        public event Action<string, string> OnPlayerJoined;
        public event Action<string, string> OnPlayerLeft;

        public ChatClient(IGrpcChannelProvider channelProvider)
        {
            _channelProvider = channelProvider;
        }

        public async Task JoinAsync(string roomId, string playerName)
        {
            var channel = _channelProvider.GetChannel();
            _hub = await StreamingHubClient.ConnectAsync<IChatHub, IChatHubReceiver>(
                channel, this);
            await _hub.JoinAsync(roomId, playerName);
            Debug.Log($"[ChatClient] Joined chat room: {roomId}");
        }

        public async Task LeaveAsync()
        {
            if (_hub != null)
            {
                await _hub.LeaveAsync();
                await _hub.DisposeAsync();
                _hub = null;
            }

            Debug.Log("[ChatClient] Left chat room");
        }

        public async Task SendMessageAsync(string content)
        {
            if (_hub != null)
            {
                await _hub.SendMessageAsync(content);
            }
        }

        public async Task<ChatMessage[]> GetRecentMessagesAsync(int count)
        {
            if (_hub != null)
            {
                return await _hub.GetRecentMessagesAsync(count);
            }

            return Array.Empty<ChatMessage>();
        }

        public async Task DeleteRoomMessagesAsync()
        {
            if (_hub != null)
            {
                await _hub.DeleteRoomMessagesAsync();
            }
        }

        // IChatHubReceiver implementations
        void IChatHubReceiver.OnMessageReceived(ChatMessage message)
        {
            OnMessageReceived?.Invoke(message);
        }

        void IChatHubReceiver.OnPlayerJoined(string userId, string playerName)
        {
            Debug.Log($"[ChatClient] Player joined: {playerName} ({userId})");
            OnPlayerJoined?.Invoke(userId, playerName);
        }

        void IChatHubReceiver.OnPlayerLeft(string userId, string playerName)
        {
            Debug.Log($"[ChatClient] Player left: {playerName} ({userId})");
            OnPlayerLeft?.Invoke(userId, playerName);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                if (_hub != null)
                {
                    _hub.DisposeAsync().GetAwaiter().GetResult();
                    _hub = null;
                }
            }
        }
    }
}
