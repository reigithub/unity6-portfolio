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
    /// マッチメイキングクライアント実装（Unary + Hub ハイブリッド）
    /// </summary>
    public class MatchmakingClient : IMatchmakingClient, IMatchmakingHubReceiver
    {
        private readonly IGrpcChannelProvider _channelProvider;

        private IMatchmakingHub _hub;
        private string _currentGameMode;
        private bool _disposed;

        public bool IsSearching { get; private set; }

        public event Action<MatchResult> OnMatchFound;
        public event Action<int> OnQueueStatusUpdated;
        public event Action<string> OnMatchmakingCancelled;

        public MatchmakingClient(IGrpcChannelProvider channelProvider)
        {
            _channelProvider = channelProvider;
        }

        public async Task<MatchmakingResponse> StartMatchmakingAsync(string gameMode)
        {
            var channel = _channelProvider.GetChannel();

            // 1. Unary RPC でキューに登録
            var matchmakingService = MagicOnionClient.Create<IMatchmakingService>(channel);
            var response = await matchmakingService.EnqueueAsync(new MatchmakingRequest { GameMode = gameMode });

            if (!response.Success)
            {
                Debug.LogWarning($"[MatchmakingClient] Failed to enqueue: {response.ErrorMessage}");
                return response;
            }

            // 2. Hub で通知を購読
            _hub = await StreamingHubClient.ConnectAsync<IMatchmakingHub, IMatchmakingHubReceiver>(
                channel, this);
            await _hub.SubscribeAsync(gameMode);

            _currentGameMode = gameMode;
            IsSearching = true;
            Debug.Log($"[MatchmakingClient] Started matchmaking for mode: {gameMode}");

            return response;
        }

        public async Task CancelMatchmakingAsync()
        {
            if (!IsSearching) return;

            var channel = _channelProvider.GetChannel();

            // 1. Unary RPC でキューから解除
            if (!string.IsNullOrEmpty(_currentGameMode))
            {
                var matchmakingService = MagicOnionClient.Create<IMatchmakingService>(channel);
                await matchmakingService.DequeueAsync(new MatchmakingRequest { GameMode = _currentGameMode });
            }

            // 2. Hub の通知購読解除
            if (_hub != null)
            {
                await _hub.UnsubscribeAsync();
                await _hub.DisposeAsync();
                _hub = null;
            }

            IsSearching = false;
            _currentGameMode = null;
            Debug.Log("[MatchmakingClient] Cancelled matchmaking");
        }

        public async Task<int> GetQueueCountAsync(string gameMode)
        {
            var channel = _channelProvider.GetChannel();
            var matchmakingService = MagicOnionClient.Create<IMatchmakingService>(channel);
            return await matchmakingService.GetQueueCountAsync(gameMode);
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

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                IsSearching = false;
                if (_hub != null)
                {
                    _hub.DisposeAsync().GetAwaiter().GetResult();
                    _hub = null;
                }
            }
        }
    }
}
