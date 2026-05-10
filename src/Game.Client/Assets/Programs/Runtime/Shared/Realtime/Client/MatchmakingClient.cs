using System;
using System.Threading;
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
        private CancellationTokenSource _monitorCts;
        private string _currentGameMode;
        private int _currentStageId;
        private bool _disposed;

        public bool IsSearching { get; private set; }

        public event Action<GameSessionStartInfo> OnMatchFound;
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

        public async Task<MatchmakingResponse> StartMatchmakingAsync(string gameMode, int stageId = 0, int matchSize = 2)
        {
            try
            {
                // Unary: キューに登録
                var response = await CreateService().EnqueueAsync(
                    new MatchmakingRequest { GameMode = gameMode, StageId = stageId, MatchSize = matchSize });

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
                _currentStageId = stageId;
                IsSearching = true;

                // 切断監視
                _monitorCts = new CancellationTokenSource();
                _ = MonitorDisconnectionAsync(_monitorCts.Token);

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
                    new MatchmakingRequest { GameMode = _currentGameMode, StageId = _currentStageId });

                _monitorCts?.Cancel();
                _monitorCts?.Dispose();
                _monitorCts = null;

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

        void IMatchmakingHubReceiver.OnMatchFound(GameSessionStartInfo info)
        {
            IsSearching = false;
            Debug.Log($"[MatchmakingClient] Match found: {info.SessionName}");
            OnMatchFound?.Invoke(info);
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

        private async Task MonitorDisconnectionAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (_hub == null) return;
                var reason = await _hub.WaitForDisconnectAsync();
                if (cancellationToken.IsCancellationRequested) return;
                if (reason.Type != DisconnectionType.CompletedNormally)
                {
                    Debug.LogWarning($"[MatchmakingClient] Unexpected disconnect: {reason.Type}");
                    IsSearching = false;
                    OnDisconnected?.Invoke($"Disconnected: {reason.Type}");
                }
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    Debug.LogWarning($"[MatchmakingClient] Disconnect monitor error: {ex.Message}");
                }
            }
        }

        public async Task DisconnectAsync()
        {
            _monitorCts?.Cancel();
            _monitorCts?.Dispose();
            _monitorCts = null;
            IsSearching = false;
            if (_hub != null)
            {
                await _hub.DisposeAsync();
                _hub = null;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                IsSearching = false;
                _monitorCts?.Cancel();
                _monitorCts?.Dispose();
                _monitorCts = null;
                if (_hub != null)
                {
                    var hub = _hub;
                    _hub = null;
                    _ = DisposeHubSafelyAsync(hub);
                }
            }
        }

        private static async Task DisposeHubSafelyAsync(IMatchmakingHub hub)
        {
            try { await hub.DisposeAsync(); }
            catch (Exception ex) { Debug.LogWarning($"[MatchmakingClient] Background dispose error: {ex.Message}"); }
        }
    }
}
