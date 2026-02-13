using System;
using Cysharp.Threading.Tasks;
using Game.Shared.Dto.Survivor;
using Game.Shared.Services.Network.Models;
using Game.Shared.Services.Network.Queue;

namespace Game.Shared.Services
{
    /// <summary>
    /// Survivor スコア・ランキング API サービス実装
    /// IApiClient を使用してサーバーのスコア/ランキングエンドポイントを呼び出す
    /// キャッシュはIApiClientが処理するため、このサービスではIResponseCache依存は不要
    /// </summary>
    public class SurvivorScoreApiService : ISurvivorScoreApiService
    {
        private const string ScoreSubmitEndpoint = "api/survivor/scores";

        private readonly IApiClient _apiClient;
        private readonly IRequestQueue _requestQueue;

        /// <summary>
        /// ランキングキャッシュオプション
        /// </summary>
        private static readonly RequestOptions RankingCacheOptions = new()
        {
            UseCache = true,
            CacheDuration = TimeSpan.FromMinutes(5),
            FallbackToCache = true,
            CacheKeyPrefix = "ranking_"
        };

        public SurvivorScoreApiService(IApiClient apiClient, IRequestQueue requestQueue)
        {
            _apiClient = apiClient;
            _requestQueue = requestQueue;
        }

        public async UniTask<ApiResponse<SurvivorScoreSubmitResponse>> SubmitScoreAsync(
            SubmitSurvivorScoreRequest request)
        {
            return await _apiClient.PostAsync<SubmitSurvivorScoreRequest, SurvivorScoreSubmitResponse>(
                ScoreSubmitEndpoint, request);
        }

        public async UniTask EnqueueSubmitScoreAsync(
            SubmitSurvivorScoreRequest request,
            RequestPriority priority = RequestPriority.High)
        {
            await _requestQueue.EnqueuePostAsync<SubmitSurvivorScoreRequest, SurvivorScoreSubmitResponse>(
                ScoreSubmitEndpoint, request, priority);
        }

        public async UniTask<ApiResponse<RankingResponse>> GetRankingAsync(
            int stageId, int limit = 100, int offset = 0)
        {
            // キャッシュはIApiClientが処理
            return await _apiClient.GetAsync<RankingResponse>(
                $"api/survivor/rankings/{stageId}?limit={limit}&offset={offset}",
                RankingCacheOptions);
        }

        public async UniTask<ApiResponse<RankingEntry>> GetMyRankAsync(int stageId)
        {
            // 自分の順位はキャッシュしない（常に最新を取得）
            return await _apiClient.GetAsync<RankingEntry>(
                $"api/survivor/rankings/{stageId}/me");
        }
    }
}
