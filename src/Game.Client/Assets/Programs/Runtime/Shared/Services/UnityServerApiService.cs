using Cysharp.Threading.Tasks;
using Game.Library.Shared.Dto;

namespace Game.Shared.Services
{
    /// <summary>
    /// Unity Dedicated Server 接続トークン取得 API サービス実装。
    /// Game.Server の POST /api/unity-server/issue-token を呼び出す。
    /// </summary>
    public class UnityServerApiService : IUnityServerApiService
    {
        private readonly IApiClient _apiClient;

        public UnityServerApiService(IApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        /// <summary>
        /// Game.Server から Unity Dedicated Server 接続用トークンを取得する。
        /// 認証済みユーザーに対して HMAC 署名付きセッショントークンを発行する。
        /// </summary>
        /// <returns>成功時はトークンとセッション名を含むレスポンス、失敗時はエラー情報。</returns>
        public async UniTask<ApiResponse<UnityServerAuthResponse>> IssueTokenAsync(int stageId = 0, int expectedPlayers = 1)
        {
            var endpoint = $"api/unity-server/issue-token?stageId={stageId}&expectedPlayers={expectedPlayers}";
            return await _apiClient.PostAsync<EmptyRequest, UnityServerAuthResponse>(
                endpoint, new EmptyRequest());
        }
    }
}
