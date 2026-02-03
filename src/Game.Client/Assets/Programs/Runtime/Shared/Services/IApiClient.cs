using Cysharp.Threading.Tasks;

namespace Game.Shared.Services
{
    /// <summary>
    /// API クライアントインターフェース
    /// UnityWebRequest ベースの HTTP 通信を抽象化
    /// </summary>
    public interface IApiClient
    {
        UniTask<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(string path, TRequest body);
        UniTask<ApiResponse<TResponse>> GetAsync<TResponse>(string path);
        void SetAuthToken(string token);
        void ClearAuthToken();
    }
}
