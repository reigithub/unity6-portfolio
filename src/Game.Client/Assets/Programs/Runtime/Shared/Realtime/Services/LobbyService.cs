using System;
using System.Threading.Tasks;
using Game.Library.Shared.Realtime.Hubs;
using MagicOnion.Client;
using UnityEngine;

namespace Game.Shared.Realtime.Services
{
    /// <summary>
    /// ロビーサービス実装（MagicOnion StreamingHub クライアント）
    /// </summary>
    public class LobbyService : ILobbyService, ILobbyHubReceiver
    {
        private readonly IMagicOnionChannelProvider _channelProvider;

        private ILobbyHub _hub;
        private bool _disposed;

        public bool IsConnected => _hub != null && !_disposed;

        public event Action<string, string> OnPlayerJoined;
        public event Action<string, string> OnPlayerLeft;
        public event Action<string, string, string> OnMessageReceived;

        public LobbyService(IMagicOnionChannelProvider channelProvider)
        {
            _channelProvider = channelProvider;
        }

        public async Task JoinLobbyAsync(string lobbyId, string playerName)
        {
            var channel = _channelProvider.GetChannel();
            _hub = await StreamingHubClient.ConnectAsync<ILobbyHub, ILobbyHubReceiver>(
                channel, this);
            await _hub.JoinAsync(lobbyId, playerName);
            Debug.Log($"[LobbyService] Joined lobby: {lobbyId}");
        }

        public async Task LeaveLobbyAsync()
        {
            if (_hub != null)
            {
                await _hub.LeaveAsync();
                await _hub.DisposeAsync();
                _hub = null;
                Debug.Log("[LobbyService] Left lobby");
            }
        }

        public async Task SendMessageAsync(string message)
        {
            if (_hub != null)
            {
                await _hub.SendMessageAsync(message);
            }
        }

        // ILobbyHubReceiver implementations
        void ILobbyHubReceiver.OnPlayerJoined(string userId, string playerName)
        {
            Debug.Log($"[LobbyService] Player joined: {playerName} ({userId})");
            OnPlayerJoined?.Invoke(userId, playerName);
        }

        void ILobbyHubReceiver.OnPlayerLeft(string userId, string playerName)
        {
            Debug.Log($"[LobbyService] Player left: {playerName} ({userId})");
            OnPlayerLeft?.Invoke(userId, playerName);
        }

        void ILobbyHubReceiver.OnMessageReceived(string userId, string playerName, string message)
        {
            OnMessageReceived?.Invoke(userId, playerName, message);
        }

        void ILobbyHubReceiver.OnLobbyClosed(string reason)
        {
            Debug.Log($"[LobbyService] Lobby closed: {reason}");
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
