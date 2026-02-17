using Cysharp.Threading.Tasks;
using Game.Library.Shared.Dto;
using Game.Shared.Services.Network.Queue;

namespace Game.Shared.Services
{
    /// <summary>
    /// Survivor スコア・ランキング API サービスインターフェース
    /// サーバーのスコア/ランキングエンドポイントとの通信を担当
    /// </summary>
    public interface ISurvivorScoreApiService
    {
        /// <summary>
        /// スコアを送信
        /// </summary>
        /// <param name="request">スコア送信リクエスト</param>
        /// <returns>送信結果（新記録かどうか、現在の順位など）</returns>
        UniTask<ApiResponse<SurvivorScoreSubmitResponse>> SubmitScoreAsync(ScoreSubmitDto request);

        /// <summary>
        /// スコア送信をキューに追加（オフライン時または送信失敗時用）
        /// </summary>
        /// <param name="request">スコア送信リクエスト</param>
        /// <param name="priority">リクエスト優先度</param>
        /// <returns>キュー追加結果</returns>
        UniTask EnqueueSubmitScoreAsync(ScoreSubmitDto request, RequestPriority priority = RequestPriority.High);

        /// <summary>
        /// ランキングを取得
        /// </summary>
        /// <param name="stageId">ステージID</param>
        /// <param name="limit">取得件数（デフォルト100）</param>
        /// <param name="offset">オフセット（デフォルト0）</param>
        /// <returns>ランキングデータ</returns>
        UniTask<ApiResponse<RankingResponse>> GetRankingAsync(int stageId, int limit = 100, int offset = 0);

        /// <summary>
        /// 自分の順位を取得
        /// </summary>
        /// <param name="stageId">ステージID</param>
        /// <returns>自分のランキングエントリ（未登録の場合はnull）</returns>
        UniTask<ApiResponse<RankingEntryDto>> GetMyRankAsync(int stageId);
    }
}
