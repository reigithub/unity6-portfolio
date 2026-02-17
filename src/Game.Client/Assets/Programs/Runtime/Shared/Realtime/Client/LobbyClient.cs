using System;
using System.Threading.Tasks;
using Game.Library.Shared.Realtime.Dto;
using Game.Library.Shared.Realtime.Hubs;
using Game.Library.Shared.Realtime.Services;
using Grpc.Core;
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
        private readonly AuthClientFilter _authFilter;
        private readonly IClientFilter[] _filters;
        private ILobbyHub _hub;
        private string _currentLobbyId;
        private bool _disposed;

        public bool IsConnected => _hub != null && !_disposed;

        public event Action<string, string> OnPlayerJoined;
        public event Action<string, string> OnPlayerLeft;
        public event Action<string, string, string> OnMessageReceived;
        public event Action<string, bool> OnPlayerReadyChanged;
        public event Action<string, string, int> OnGameStarting;
        public event Action<string> OnDisconnected;

        public LobbyClient(
            IGrpcChannelProvider channelProvider,
            AuthClientFilter authFilter)
        {
            _channelProvider = channelProvider;
            _authFilter = authFilter;
            _filters = new IClientFilter[] { authFilter };
        }

        private ILobbyService CreateService()
        {
            var channel = _channelProvider.GetChannel();
            return MagicOnionClient.Create<ILobbyService>(channel, _filters);
        }

        public async Task<CreateLobbyResponse> CreateLobbyAsync(CreateLobbyRequest request)
        {
            try
            {
                var response = await CreateService().CreateLobbyAsync(request);
                Debug.Log($"[LobbyClient] Created lobby: {response.LobbyId}");
                return response;
            }
            catch (RpcException ex)
            {
                Debug.LogError($"[LobbyClient] RPC error in CreateLobby: {ex.StatusCode} - {ex.Status.Detail}");
                throw;
            }
        }

        public async Task<LobbyInfo> JoinLobbyAsync(string lobbyId, string playerName)
        {
            try
            {
                var lobby = await CreateService().JoinLobbyAsync(lobbyId, playerName);
                Debug.Log($"[LobbyClient] Joined lobby: {lobbyId}");
                return lobby;
            }
            catch (RpcException ex)
            {
                Debug.LogError($"[LobbyClient] RPC error in JoinLobby: {ex.StatusCode} - {ex.Status.Detail}");
                throw;
            }
        }

        public async Task ConnectToLobbyAsync(string lobbyId, string playerName)
        {
            try
            {
                var channel = _channelProvider.GetChannel();

                // StreamingHub は IClientFilter 非対応のため CallOptions で認証ヘッダーを渡す
                var options = StreamingHubClientOptions.CreateWithDefault(
                        callOptions: new CallOptions(headers: _authFilter.CreateAuthMetadata()))
                    .WithClientHeartbeatInterval(TimeSpan.FromSeconds(30))
                    .WithClientHeartbeatTimeout(TimeSpan.FromSeconds(10));

                _hub = await StreamingHubClient.ConnectAsync<ILobbyHub, ILobbyHubReceiver>(
                    channel, this, options);
                await _hub.ConnectAsync(lobbyId, playerName);
                _currentLobbyId = lobbyId;
                Debug.Log($"[LobbyClient] Connected to lobby hub: {lobbyId}");

                // 切断監視（fire-and-forget）
                _ = MonitorDisconnectionAsync();
            }
            catch (RpcException ex)
            {
                Debug.LogError($"[LobbyClient] RPC error in ConnectToLobby: {ex.StatusCode} - {ex.Status.Detail}");
                throw;
            }
        }

        public async Task LeaveLobbyAsync()
        {
            try
            {
                if (_hub != null)
                {
                    await _hub.LeaveAsync();
                    await _hub.DisposeAsync();
                    _hub = null;
                }

                if (!string.IsNullOrEmpty(_currentLobbyId))
                {
                    await CreateService().LeaveLobbyAsync(_currentLobbyId);
                    _currentLobbyId = null;
                }

                Debug.Log("[LobbyClient] Left lobby");
            }
            catch (RpcException ex)
            {
                Debug.LogWarning($"[LobbyClient] RPC error in LeaveLobby: {ex.StatusCode}");
            }
        }

        public async Task<LobbyInfo[]> SearchLobbiesAsync(string gameMode, int maxResults)
        {
            try
            {
                return await CreateService().SearchLobbiesAsync(gameMode, maxResults);
            }
            catch (RpcException ex)
            {
                Debug.LogError($"[LobbyClient] RPC error in SearchLobbies: {ex.StatusCode}");
                return Array.Empty<LobbyInfo>();
            }
        }

        public async Task SendMessageAsync(string message)
        {
            try
            {
                if (_hub != null)
                {
                    await _hub.SendMessageAsync(message);
                }
            }
            catch (RpcException ex)
            {
                Debug.LogWarning($"[LobbyClient] RPC error in SendMessage: {ex.StatusCode}");
            }
        }

        public async Task SetReadyAsync(bool isReady)
        {
            try
            {
                if (_hub != null)
                {
                    await _hub.SetReadyAsync(isReady);
                }
            }
            catch (RpcException ex)
            {
                Debug.LogWarning($"[LobbyClient] RPC error in SetReady: {ex.StatusCode}");
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

        private async Task MonitorDisconnectionAsync()
        {
            try
            {
                if (_hub == null) return;
                var reason = await _hub.WaitForDisconnectAsync();
                if (reason.Type != DisconnectionType.CompletedNormally)
                {
                    Debug.LogWarning($"[LobbyClient] Unexpected disconnect: {reason.Type}");
                    OnDisconnected?.Invoke($"Disconnected: {reason.Type}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyClient] Disconnect monitor error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                if (_hub != null)
                {
                    try { _hub.DisposeAsync().GetAwaiter().GetResult(); }
                    catch (Exception ex) { Debug.LogWarning($"[LobbyClient] Dispose error: {ex.Message}"); }
                    _hub = null;
                }
            }
        }
    }
}
