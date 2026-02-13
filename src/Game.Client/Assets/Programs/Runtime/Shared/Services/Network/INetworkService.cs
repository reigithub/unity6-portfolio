using Game.Shared.Services.Network.Policies;
using R3;

namespace Game.Shared.Services.Network
{
    /// <summary>
    /// ネットワーク接続状態とサーキットブレーカーを管理するゲートウェイ
    /// API通信は行わない（IApiClientの責務）
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
        /// サーキットブレーカーでリクエストを実行可能か
        /// </summary>
        bool CanExecute { get; }

        /// <summary>
        /// サーキットブレーカーの現在の状態
        /// </summary>
        CircuitState CircuitState { get; }

        /// <summary>
        /// サーキットブレーカーの状態変更イベント（R3 Observable）
        /// </summary>
        Observable<CircuitState> OnCircuitStateChanged { get; }

        /// <summary>
        /// 成功を記録（IApiClientから呼び出し）
        /// </summary>
        void RecordSuccess();

        /// <summary>
        /// 失敗を記録（IApiClientから呼び出し）
        /// </summary>
        void RecordFailure();

        /// <summary>
        /// サーキットブレーカーを手動でリセット
        /// </summary>
        void ResetCircuitBreaker();
    }
}
