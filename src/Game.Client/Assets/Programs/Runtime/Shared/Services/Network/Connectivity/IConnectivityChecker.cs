using System;

namespace Game.Shared.Services.Network.Connectivity
{
    /// <summary>
    /// ネットワーク接続状態を監視するインターフェース
    /// </summary>
    public interface IConnectivityChecker : IDisposable
    {
        /// <summary>
        /// 現在のネットワーク接続状態
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 接続状態変更時のイベント
        /// </summary>
        IObservable<bool> OnConnectivityChanged { get; }

        /// <summary>
        /// 接続監視を開始
        /// </summary>
        void StartMonitoring();

        /// <summary>
        /// 接続監視を停止
        /// </summary>
        void StopMonitoring();

        /// <summary>
        /// 接続状態を即座にチェック
        /// </summary>
        /// <returns>接続されている場合はtrue</returns>
        bool CheckConnectivity();
    }
}
