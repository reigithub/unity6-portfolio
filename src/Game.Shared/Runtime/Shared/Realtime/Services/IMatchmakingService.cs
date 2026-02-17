using System.Threading.Tasks;
using Game.Library.Shared.Realtime.Dto;
using MagicOnion;

namespace Game.Library.Shared.Realtime.Services
{
    /// <summary>
    /// マッチメイキング Unary RPC サービスインターフェース
    /// </summary>
    public interface IMatchmakingService : IService<IMatchmakingService>
    {
        /// <summary>
        /// マッチメイキングキューに登録
        /// </summary>
        UnaryResult<MatchmakingResponse> EnqueueAsync(MatchmakingRequest request);

        /// <summary>
        /// マッチメイキングキューから解除
        /// </summary>
        UnaryResult<MatchmakingResponse> DequeueAsync(MatchmakingRequest request);

        /// <summary>
        /// 指定ゲームモードのキュー人数を取得
        /// </summary>
        UnaryResult<int> GetQueueCountAsync(string gameMode);
    }
}
