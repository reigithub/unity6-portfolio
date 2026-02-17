using System;
using System.Threading.Tasks;
using Game.Library.Shared.Realtime.Dto;
using Game.Library.Shared.Realtime.Hubs;
using Game.Library.Shared.Realtime.Services;
using MagicOnion.Client;
using UnityEngine;

namespace Game.Shared.Realtime.Client
{
    /// <summary>
    /// ロビークライアント実装（Unary + Hub ハイブリッド）
    /// </summary>
    public class LobbyClient : ILobbyClient, ILobbyHubReceiver
    {
        private readonly IGrpcChannelProvider _channelProvider;

        private ILobbyHub _hub;
        private string _currentLobbyId;
        private bool _disposed;

        public bool IsConnected => _hub != null && !_disposed;

        public event Action<string, string> OnPlayerJoined;
        public event Action<string, string> OnPlayerLeft;
        public event Action<string, string, string> OnMessageReceived;
        public event Action<string, bool> OnPlayerReadyChanged;
        public event Action<string, string, int> OnGameStarting;

        public LobbyClient(IGrpcChannelProvider channelProvider)
        {
            _channelProvider = channelProvider;
        }

        public async Task<CreateLobbyResponse> CreateLobbyAsync(CreateLobbyRequest request)
        {
            var channel = _channelProvider.GetChannel();
            var lobbyService = MagicOnionClient.Create<ILobbyService>(channel);
            var response = await lobbyService.CreateLobbyAsync(request);
            Debug.Log($"[LobbyClient] Created lobby: {response.LobbyId}");
            return response;
        }

        public async Task<LobbyInfo> JoinLobbyAsync(string lobbyId, string playerName)
        {
            var channel = _channelProvider.GetChannel();
            var lobbyService = MagicOnionClient.Create<ILobbyService>(channel);
            var lobby = await lobbyService.JoinLobbyAsync(lobbyId, playerName);
            Debug.Log($"[LobbyClient] Joined lobby: {lobbyId}");
            return lobby;
        }

        public async Task ConnectToLobbyAsync(string lobbyId, string playerName)
        {
            var channel = _channelProvider.GetChannel();
            _hub = await StreamingHubClient.ConnectAsync<ILobbyHub, ILobbyHubReceiver>(
                channel, this);
            await _hub.ConnectAsync(lobbyId, playerName);
            _currentLobbyId = lobbyId;
            Debug.Log($"[LobbyClient] Connected to lobby hub: {lobbyId}");
        }

        public async Task LeaveLobbyAsync()
        {
            if (_hub != null)
            {
                await _hub.LeaveAsync();
                await _hub.DisposeAsync();
                _hub = null;
            }

            if (!string.IsNullOrEmpty(_currentLobbyId))
            {
                var channel = _channelProvider.GetChannel();
                var lobbyService = MagicOnionClient.Create<ILobbyService>(channel);
                await lobbyService.LeaveLobbyAsync(_currentLobbyId);
                _currentLobbyId = null;
            }

            Debug.Log("[LobbyClient] Left lobby");
        }

        public async Task<LobbyInfo[]> SearchLobbiesAsync(string gameMode, int maxResults)
        {
            var channel = _channelProvider.GetChannel();
            var lobbyService = MagicOnionClient.Create<ILobbyService>(channel);
            return await lobbyService.SearchLobbiesAsync(gameMode, maxResults);
        }

        public async Task SendMessageAsync(string message)
        {
            if (_hub != null)
            {
                await _hub.SendMessageAsync(message);
            }
        }

        public async Task SetReadyAsync(bool isReady)
        {
            if (_hub != null)
            {
                await _hub.SetReadyAsync(isReady);
            }
        }

        // ILobbyHubReceiver implementations
        void ILobbyHubReceiver.OnPlayerJoined(string userId, string playerName)
        {
            Debug.Log($"[LobbyClient] Player joined: {playerName} ({userId})");
            OnPlayerJoined?.Invoke(userId, playerName);
        }

        void ILobbyHubReceiver.OnPlayerLeft(string userId, string playerName)
        {
            Debug.Log($"[LobbyClient] Player left: {playerName} ({userId})");
            OnPlayerLeft?.Invoke(userId, playerName);
        }

        void ILobbyHubReceiver.OnMessageReceived(string userId, string playerName, string message)
        {
            OnMessageReceived?.Invoke(userId, playerName, message);
        }

        void ILobbyHubReceiver.OnLobbyClosed(string reason)
        {
            Debug.Log($"[LobbyClient] Lobby closed: {reason}");
        }

        void ILobbyHubReceiver.OnPlayerReadyChanged(string userId, bool isReady)
        {
            Debug.Log($"[LobbyClient] Player {userId} ready={isReady}");
            OnPlayerReadyChanged?.Invoke(userId, isReady);
        }

        void ILobbyHubReceiver.OnGameStarting(string matchId, string serverAddress, int serverPort)
        {
            Debug.Log($"[LobbyClient] Game starting: {matchId} @ {serverAddress}:{serverPort}");
            OnGameStarting?.Invoke(matchId, serverAddress, serverPort);
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
