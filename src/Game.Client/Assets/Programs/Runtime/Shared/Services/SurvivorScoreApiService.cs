using Cysharp.Threading.Tasks;
using Game.Shared.Dto.Survivor;

namespace Game.Shared.Services
{
    /// <summary>
    /// Survivor スコア・ランキング API サービス実装
    /// IApiClient を使用してサーバーのスコア/ランキングエンドポイントを呼び出す
    /// </summary>
    public class SurvivorScoreApiService : ISurvivorScoreApiService
    {
        private readonly IApiClient _apiClient;

        public SurvivorScoreApiService(IApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public async UniTask<ApiResponse<SurvivorScoreSubmitResponse>> SubmitScoreAsync(
            SubmitSurvivorScoreRequest request)
        {
            return await _apiClient.PostAsync<SubmitSurvivorScoreRequest, SurvivorScoreSubmitResponse>(
                "api/survivor/scores", request);
        }

        public async UniTask<ApiResponse<RankingResponse>> GetRankingAsync(
            int stageId, int limit = 100, int offset = 0)
        {
            return await _apiClient.GetAsync<RankingResponse>(
                $"api/survivor/rankings/{stageId}?limit={limit}&offset={offset}");
        }

        public async UniTask<ApiResponse<RankingEntry>> GetMyRankAsync(int stageId)
        {
            return await _apiClient.GetAsync<RankingEntry>(
                $"api/survivor/rankings/{stageId}/me");
        }
    }
}
