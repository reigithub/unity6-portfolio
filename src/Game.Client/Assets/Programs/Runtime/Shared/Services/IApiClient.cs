using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Shared.Services.Network.Models;

namespace Game.Shared.Services
{
    /// <summary>
    /// API クライアントインターフェース
    /// UnityWebRequest ベースの HTTP 通信を抽象化
    /// </summary>
    public interface IApiClient
    {
        /// <summary>
        /// POSTリクエストを送信
        /// </summary>
        /// <typeparam name="TRequest">リクエストボディの型</typeparam>
        /// <typeparam name="TResponse">レスポンスの型</typeparam>
        /// <param name="path">APIパス</param>
        /// <param name="body">リクエストボディ</param>
        /// <param name="options">リクエストオプション（省略可）</param>
        /// <param name="cancellationToken">キャンセルトークン（省略可）</param>
        /// <returns>APIレスポンス</returns>
        UniTask<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(
            string path,
            TRequest body,
            RequestOptions options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// GETリクエストを送信
        /// </summary>
        /// <typeparam name="TResponse">レスポンスの型</typeparam>
        /// <param name="path">APIパス</param>
        /// <param name="options">リクエストオプション（省略可）</param>
        /// <param name="cancellationToken">キャンセルトークン（省略可）</param>
        /// <returns>APIレスポンス</returns>
        UniTask<ApiResponse<TResponse>> GetAsync<TResponse>(
            string path,
            RequestOptions options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// DELETEリクエストを送信
        /// </summary>
        /// <typeparam name="TResponse">レスポンスの型</typeparam>
        /// <param name="path">APIパス</param>
        /// <param name="options">リクエストオプション（省略可）</param>
        /// <param name="cancellationToken">キャンセルトークン（省略可）</param>
        /// <returns>APIレスポンス</returns>
        UniTask<ApiResponse<TResponse>> DeleteAsync<TResponse>(
            string path,
            RequestOptions options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 認証トークンを設定
        /// </summary>
        /// <param name="token">Bearer トークン</param>
        void SetAuthToken(string token);

        /// <summary>
        /// 認証トークンをクリア
        /// </summary>
        void ClearAuthToken();

        /// <summary>
        /// サーバーから配布された署名鍵を設定
        /// </summary>
        /// <param name="base64Key">Base64エンコードされた署名鍵</param>
        void SetSigningKey(string base64Key);

        /// <summary>
        /// 署名鍵をクリア
        /// </summary>
        void ClearSigningKey();
    }
}
