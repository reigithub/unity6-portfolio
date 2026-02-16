using System;
using System.Threading.Tasks;
using Game.Library.Shared.Realtime.Dto;
using Game.Library.Shared.Realtime.Hubs;

namespace Game.Shared.Realtime.Client
{
    /// <summary>
    /// マッチメイキングクライアントインターフェース（Unary + Hub ハイブリッド）
    /// </summary>
    public interface IMatchmakingClient : IDisposable
    {
        /// <summary>
        /// マッチメイキング中かどうか
        /// </summary>
        bool IsSearching { get; }

        /// <summary>
        /// マッチメイキング開始（Unary で登録 → Hub で通知購読）
        /// </summary>
        Task<MatchmakingResponse> StartMatchmakingAsync(string gameMode);

        /// <summary>
        /// マッチメイキングキャンセル（Unary で解除 → Hub 購読解除）
        /// </summary>
        Task CancelMatchmakingAsync();

        /// <summary>
        /// キュー人数取得（Unary のみ）
        /// </summary>
        Task<int> GetQueueCountAsync(string gameMode);

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
