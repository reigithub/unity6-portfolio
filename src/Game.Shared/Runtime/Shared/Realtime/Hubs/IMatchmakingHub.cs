using System.Threading.Tasks;
using MagicOnion;
using MessagePack;

namespace Game.Library.Shared.Realtime.Hubs
{
    /// <summary>
    /// マッチメイキング結果
    /// </summary>
    [MessagePackObject]
    public class MatchResult
    {
        [Key(0)]
        public string MatchId { get; set; } = string.Empty;

        [Key(1)]
        public string[] PlayerIds { get; set; } = System.Array.Empty<string>();

        [Key(2)]
        public string ServerAddress { get; set; } = string.Empty;

        [Key(3)]
        public int ServerPort { get; set; }

        [Key(4)]
        public string SessionToken { get; set; } = string.Empty;
    }

    /// <summary>
    /// マッチメイキングHub クライアント受信インターフェース
    /// </summary>
    public interface IMatchmakingHubReceiver
    {
        /// <summary>
        /// マッチメイキングキューに入った通知
        /// </summary>
        void OnMatchmakingStarted(int estimatedWaitSeconds);

        /// <summary>
        /// マッチが成立した通知
        /// </summary>
        void OnMatchFound(MatchResult result);

        /// <summary>
        /// マッチメイキングがキャンセルされた通知
        /// </summary>
        void OnMatchmakingCancelled(string reason);

        /// <summary>
        /// 待機人数更新通知
        /// </summary>
        void OnQueueStatusUpdated(int playersInQueue);
    }

    /// <summary>
    /// マッチメイキングHub サーバー送信インターフェース（StreamingHub）
    /// 通知専用：キュー操作は Unary IMatchmakingService 経由
    /// </summary>
    public interface IMatchmakingHub : IStreamingHub<IMatchmakingHub, IMatchmakingHubReceiver>
    {
        /// <summary>
        /// マッチメイキング通知を購読
        /// </summary>
        ValueTask SubscribeAsync(string gameMode);

        /// <summary>
        /// マッチメイキング通知の購読解除
        /// </summary>
        ValueTask UnsubscribeAsync();
    }
}
