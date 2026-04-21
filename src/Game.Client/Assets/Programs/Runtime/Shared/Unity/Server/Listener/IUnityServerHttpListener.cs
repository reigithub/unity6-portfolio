using System;

namespace Game.Shared.Unity.Server
{
    /// <summary>
    /// Dedicated Server 向け HTTP リスナーのインターフェース。
    /// セッション作成リクエストの受付と DS ステータス管理を担う。
    /// </summary>
    public interface IUnityServerHttpListener : IDisposable
    {
        /// <summary>現在の DS ステータス。"idle" または "active"。</summary>
        string Status { get; }

        /// <summary>現在実行中の Fusion セッション名（SessionName）。idle 時は null。</summary>
        string CurrentSessionName { get; }

        /// <summary>起動からの経過秒数。</summary>
        long UptimeSeconds { get; }

        /// <summary>
        /// HTTP リスナーをバックグラウンドスレッドで起動する。
        /// </summary>
        void Start();

        /// <summary>
        /// メインスレッドからセッション作成リクエストをデキューする。
        /// SurvivorServerGameLoop から一定間隔で呼ぶ。
        /// </summary>
        /// <param name="request">デキューしたリクエスト。</param>
        /// <returns>リクエストが存在した場合は true。</returns>
        bool TryDequeueSessionRequest(out UnityServerSessionRequest request);

        /// <summary>
        /// セッション状態を active に更新する。
        /// メインスレッドから呼ぶ。
        /// </summary>
        /// <param name="sessionName">開始した Fusion セッション名（SessionName）。</param>
        void SetSessionActive(string sessionName);

        /// <summary>
        /// セッション状態を idle に戻す。
        /// メインスレッドから呼ぶ。
        /// </summary>
        void SetSessionIdle();
    }
}
