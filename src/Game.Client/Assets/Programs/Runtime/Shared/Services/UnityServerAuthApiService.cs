using Cysharp.Threading.Tasks;
using Game.Library.Shared.Dto;

namespace Game.Shared.Services
{
    /// <summary>
    /// Unity Dedicated Server 接続トークン取得 API サービス実装。
    /// Game.Server の POST /api/unity-server-auth/issue-token を呼び出す。
    /// </summary>
    public class UnityServerAuthApiService : IUnityServerAuthApiService
    {
        private readonly IApiClient _apiClient;

        public UnityServerAuthApiService(IApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        /// <summary>
        /// Game.Server から Unity Dedicated Server 接続用トークンを取得する。
        /// 認証済みユーザーに対して HMAC 署名付きセッショントークンを発行する。
        /// </summary>
        /// <returns>成功時はトークンとセッション名を含むレスポンス、失敗時はエラー情報。</returns>
        public async UniTask<ApiResponse<UnityServerAuthResponse>> IssueTokenAsync()
        {
            return await _apiClient.PostAsync<EmptyRequest, UnityServerAuthResponse>(
                "api/unity-server-auth/issue-token", new EmptyRequest());
        }
    }
}
