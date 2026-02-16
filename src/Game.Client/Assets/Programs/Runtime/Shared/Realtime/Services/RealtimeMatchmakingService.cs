using System;
using System.Threading.Tasks;
using Game.Library.Shared.Realtime.Hubs;
using MagicOnion.Client;
using UnityEngine;

namespace Game.Shared.Realtime.Services
{
    /// <summary>
    /// リアルタイムマッチメイキングサービス実装（MagicOnion StreamingHub クライアント）
    /// </summary>
    public class RealtimeMatchmakingService : IRealtimeMatchmakingService, IMatchmakingHubReceiver
    {
        private readonly IMagicOnionChannelProvider _channelProvider;

        private IMatchmakingHub _hub;
        private bool _disposed;

        public bool IsSearching { get; private set; }

        public event Action<MatchResult> OnMatchFound;
        public event Action<int> OnQueueStatusUpdated;
        public event Action<string> OnMatchmakingCancelled;

        public RealtimeMatchmakingService(IMagicOnionChannelProvider channelProvider)
        {
            _channelProvider = channelProvider;
        }

        public async Task StartMatchmakingAsync(string gameMode)
        {
            var channel = _channelProvider.GetChannel();
            _hub = await StreamingHubClient.ConnectAsync<IMatchmakingHub, IMatchmakingHubReceiver>(
                channel, this);
            await _hub.StartMatchmakingAsync(gameMode);
            IsSearching = true;
            Debug.Log($"[RealtimeMatchmaking] Started matchmaking for mode: {gameMode}");
        }

        public async Task CancelMatchmakingAsync()
        {
            if (_hub != null && IsSearching)
            {
                await _hub.CancelMatchmakingAsync();
                IsSearching = false;
                Debug.Log("[RealtimeMatchmaking] Cancelled matchmaking");
            }
        }

        // IMatchmakingHubReceiver implementations
        void IMatchmakingHubReceiver.OnMatchmakingStarted(int estimatedWaitSeconds)
        {
            Debug.Log($"[RealtimeMatchmaking] Matchmaking started. Estimated wait: {estimatedWaitSeconds}s");
        }

        void IMatchmakingHubReceiver.OnMatchFound(MatchResult result)
        {
            IsSearching = false;
            Debug.Log($"[RealtimeMatchmaking] Match found: {result.MatchId}");
            OnMatchFound?.Invoke(result);
        }

        void IMatchmakingHubReceiver.OnMatchmakingCancelled(string reason)
        {
            IsSearching = false;
            Debug.Log($"[RealtimeMatchmaking] Matchmaking cancelled: {reason}");
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
