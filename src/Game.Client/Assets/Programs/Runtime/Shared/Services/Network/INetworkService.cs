using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Shared.Services.Network.Models;
using Game.Shared.Services.Network.Policies;
using R3;

namespace Game.Shared.Services.Network
{
    /// <summary>
    /// 統一ネットワークサービスのインターフェース
    /// 接続監視、キャッシュ、リトライ、サーキットブレーカーを統合
    /// </summary>
    public interface INetworkService
    {
        /// <summary>
        /// 現在のネットワーク接続状態
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 接続状態変更時のイベント（R3 Observable）
        /// </summary>
        Observable<bool> OnConnectivityChanged { get; }

        /// <summary>
        /// サーキットブレーカーの現在の状態
        /// </summary>
        CircuitState CircuitState { get; }

        /// <summary>
        /// サーキットブレーカーの状態変更イベント（R3 Observable）
        /// </summary>
        Observable<CircuitState> OnCircuitStateChanged { get; }

        /// <summary>
        /// サーキットブレーカーを手動でリセット
        /// </summary>
        void ResetCircuitBreaker();

        /// <summary>
        /// GETリクエストを送信
        /// </summary>
        /// <typeparam name="T">レスポンスの型</typeparam>
        /// <param name="endpoint">APIエンドポイント</param>
        /// <param name="options">リクエストオプション</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>ネットワーク結果</returns>
        UniTask<NetworkResult<T>> GetAsync<T>(
            string endpoint,
            RequestOptions options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// POSTリクエストを送信
        /// </summary>
        /// <typeparam name="TRequest">リクエストの型</typeparam>
        /// <typeparam name="TResponse">レスポンスの型</typeparam>
        /// <param name="endpoint">APIエンドポイント</param>
        /// <param name="body">リクエストボディ</param>
        /// <param name="options">リクエストオプション</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>ネットワーク結果</returns>
        UniTask<NetworkResult<TResponse>> PostAsync<TRequest, TResponse>(
            string endpoint,
            TRequest body,
            RequestOptions options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// DELETEリクエストを送信
        /// </summary>
        /// <typeparam name="T">レスポンスの型</typeparam>
        /// <param name="endpoint">APIエンドポイント</param>
        /// <param name="options">リクエストオプション</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>ネットワーク結果</returns>
        UniTask<NetworkResult<T>> DeleteAsync<T>(
            string endpoint,
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
        /// キャッシュをクリア
        /// </summary>
        UniTask ClearCacheAsync();
    }
}
