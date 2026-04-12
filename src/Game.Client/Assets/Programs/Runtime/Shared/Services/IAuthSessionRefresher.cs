using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Shared.Services
{
    /// <summary>
    /// 認証セッションの refresh を自律的に維持する background service。
    ///
    /// 責務:
    /// - 周期的 proactive refresh (5 分毎 check + 50 分閾値)
    /// - Reactive trigger (network 復帰、app foreground 復帰)
    /// - 明示要求への dedup された応答
    ///
    /// 非責務:
    /// - 初回 refresh (<see cref="Game.MVP.Survivor.SurvivorGameRunner"/>.TryRefreshSessionAsync が担当)
    /// - Guest login fallback (各 scene の EnsureValidSessionAsync が担当)
    /// - 401 reactive retry (別 phase で UnityApiClient interceptor を追加する場合に対応)
    /// </summary>
    public interface IAuthSessionRefresher
    {
        /// <summary>Refresh が進行中の瞬間のみ true (デバッグ・UI indicator 用)。</summary>
        bool IsRefreshing { get; }

        /// <summary>最後に refresh を試行した時刻 (成功/失敗問わず)。</summary>
        DateTime? LastRefreshAttemptAt { get; }

        /// <summary>最後の refresh を発火した trigger 種別。</summary>
        RefreshTrigger LastRefreshTrigger { get; }

        /// <summary>refresh 試行の累計回数 (成功/失敗問わず)。</summary>
        int TotalRefreshCount { get; }

        /// <summary>refresh 失敗の累計回数。</summary>
        int FailedRefreshCount { get; }

        /// <summary>
        /// 必要であれば refresh を実行し、完了を待つ。
        /// <see cref="IAuthSessionService.IsRecentlyRefreshed()"/> で fresh と判定されたら即 true を返す。
        /// 並列呼び出しは内部で 1 回に dedup される。
        /// Scene/Dialog から呼び出される主 API。
        /// </summary>
        /// <param name="ct">キャンセル token。</param>
        /// <returns>refresh 成功、または既に fresh で skip された場合は true</returns>
        UniTask<bool> EnsureFreshAsync(CancellationToken ct = default);
    }

    /// <summary>Refresh を発火した契機。観察・ログ用。</summary>
    public enum RefreshTrigger
    {
        /// <summary>周期 loop 5 分毎 check で threshold 超過を検出</summary>
        Periodic,

        /// <summary>Offline → Online への network 復帰</summary>
        NetworkRecovery,

        /// <summary>App が background → foreground に復帰</summary>
        AppFocus,

        /// <summary>scene/dialog からの明示 EnsureFreshAsync 呼び出し</summary>
        Explicit,
    }
}
