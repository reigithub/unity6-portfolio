using System;
using System.Threading.Tasks;
using Game.Library.Shared.Realtime.Hubs;

namespace Game.Shared.Realtime.Services
{
    /// <summary>
    /// リアルタイムマッチメイキングサービスインターフェース（クライアント側）
    /// </summary>
    public interface IRealtimeMatchmakingService : IDisposable
    {
        /// <summary>
        /// マッチメイキング中かどうか
        /// </summary>
        bool IsSearching { get; }

        /// <summary>
        /// マッチメイキング開始
        /// </summary>
        Task StartMatchmakingAsync(string gameMode);

        /// <summary>
        /// マッチメイキングキャンセル
        /// </summary>
        Task CancelMatchmakingAsync();

        /// <summary>
        /// マッチ成立イベント
        /// </summary>
        event Action<MatchResult> OnMatchFound;

        /// <summary>
        /// キュー人数更新イベント
        /// </summary>
        event Action<int> OnQueueStatusUpdated;

        /// <summary>
        /// マッチメイキングキャンセルイベント
        /// </summary>
        event Action<string> OnMatchmakingCancelled;
    }
}
