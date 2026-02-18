using System;
using System.Threading.Tasks;
using Game.Library.Shared.Dto;
using Game.Library.Shared.Realtime.Hubs;
using Game.Library.Shared.Realtime.Services;
using Grpc.Core;
using MagicOnion.Client;
using UnityEngine;

namespace Game.Shared.Realtime.Client
{
    /// <summary>
    /// マッチメイキングクライアント実装（Unary + Hub ハイブリッド）
    /// </summary>
    public class MatchmakingClient : IMatchmakingClient, IMatchmakingHubReceiver
    {
        private readonly IGrpcChannelProvider _channelProvider;
        private readonly AuthClientFilter _authFilter;
        private readonly IClientFilter[] _filters;
        private IMatchmakingHub _hub;
        private string _currentGameMode;
        private bool _disposed;

        public bool IsSearching { get; private set; }

        public event Action<MatchResult> OnMatchFound;
        public event Action<int> OnQueueStatusUpdated;
        public event Action<string> OnMatchmakingCancelled;
        public event Action<string> OnDisconnected;

        public MatchmakingClient(
            IGrpcChannelProvider channelProvider,
            AuthClientFilter authFilter)
        {
            _channelProvider = channelProvider;
            _authFilter = authFilter;
            _filters = new IClientFilter[] { authFilter };
        }

        private IMatchmakingService CreateService()
        {
            var channel = _channelProvider.GetChannel();
            return MagicOnionClient.Create<IMatchmakingService>(channel, _filters);
        }

        public async Task<MatchmakingResponse> StartMatchmakingAsync(string gameMode)
        {
            try
            {
                // Unary: キューに登録
                var response = await CreateService().EnqueueAsync(
                    new MatchmakingRequest { GameMode = gameMode });

                if (!response.Success)
                {
                    Debug.LogWarning($"[MatchmakingClient] Enqueue failed: {response.ErrorMessage}");
                    return response;
                }

                // Hub: 通知購読（認証 + Heartbeat）
                // StreamingHub は IClientFilter 非対応のため CallOptions で認証ヘッダーを渡す
                var options = StreamingHubClientOptions.CreateWithDefault(
                        callOptions: new CallOptions(headers: _authFilter.CreateAuthMetadata()))
                    .WithClientHeartbeatInterval(TimeSpan.FromSeconds(30))
                    .WithClientHeartbeatTimeout(TimeSpan.FromSeconds(10));

                var channel = _channelProvider.GetChannel();
                _hub = await StreamingHubClient.ConnectAsync<IMatchmakingHub, IMatchmakingHubReceiver>(
                    channel, this, options);
                await _hub.SubscribeAsync(gameMode);

                _currentGameMode = gameMode;
                IsSearching = true;

                // 切断監視（fire-and-forget）
                _ = MonitorDisconnectionAsync();

                return response;
            }
            catch (RpcException ex)
            {
                Debug.LogError($"[MatchmakingClient] RPC error in StartMatchmaking: {ex.StatusCode} - {ex.Status.Detail}");
                return new MatchmakingResponse
                {
                    Success = false,
                    ErrorMessage = ex.Status.Detail
                };
            }
        }

        public async Task CancelMatchmakingAsync()
        {
            if (!IsSearching) return;

            try
            {
                await CreateService().DequeueAsync(
                    new MatchmakingRequest { GameMode = _currentGameMode });

                if (_hub != null)
                {
                    await _hub.UnsubscribeAsync();
                    await _hub.DisposeAsync();
                    _hub = null;
                }
            }
            catch (RpcException ex)
            {
                Debug.LogWarning($"[MatchmakingClient] RPC error in Cancel: {ex.StatusCode}");
            }
            finally
            {
                IsSearching = false;
            }
        }

        public async Task<int> GetQueueCountAsync(string gameMode)
        {
            try
            {
                return await CreateService().GetQueueCountAsync(gameMode);
            }
            catch (RpcException ex)
            {
                Debug.LogError($"[MatchmakingClient] RPC error in GetQueueCount: {ex.StatusCode}");
                return -1;
            }
        }

        // IMatchmakingHubReceiver implementations
        void IMatchmakingHubReceiver.OnMatchmakingStarted(int estimatedWaitSeconds)
        {
            Debug.Log($"[MatchmakingClient] Matchmaking started. Estimated wait: {estimatedWaitSeconds}s");
        }

        void IMatchmakingHubReceiver.OnMatchFound(MatchResult result)
        {
            IsSearching = false;
            Debug.Log($"[MatchmakingClient] Match found: {result.MatchId}");
            OnMatchFound?.Invoke(result);
        }

        void IMatchmakingHubReceiver.OnMatchmakingCancelled(string reason)
        {
            IsSearching = false;
            Debug.Log($"[MatchmakingClient] Matchmaking cancelled: {reason}");
            OnMatchmakingCancelled?.Invoke(reason);
        }

        void IMatchmakingHubReceiver.OnQueueStatusUpdated(int playersInQueue)
        {
            OnQueueStatusUpdated?.Invoke(playersInQueue);
        }

        private async Task MonitorDisconnectionAsync()
        {
            try
            {
                if (_hub == null) return;
                var reason = await _hub.WaitForDisconnectAsync();
                if (reason.Type != DisconnectionType.CompletedNormally)
                {
                    Debug.LogWarning($"[MatchmakingClient] Unexpected disconnect: {reason.Type}");
                    IsSearching = false;
                    OnDisconnected?.Invoke($"Disconnected: {reason.Type}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MatchmakingClient] Disconnect monitor error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                IsSearching = false;
                if (_hub != null)
                {
                    try { _hub.DisposeAsync().GetAwaiter().GetResult(); }
                    catch (Exception ex) { Debug.LogWarning($"[MatchmakingClient] Dispose error: {ex.Message}"); }
                    _hub = null;
                }
            }
        }
    }
}
