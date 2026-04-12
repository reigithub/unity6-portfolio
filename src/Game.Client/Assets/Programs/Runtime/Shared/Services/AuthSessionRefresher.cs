using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Shared.Bootstrap;
using Game.Shared.Services.Network;
using Game.Shared.Signals.Survivor;
using MessagePipe;
using R3;
using UnityEngine;
using VContainer.Unity;

namespace Game.Shared.Services
{
    /// <summary>
    /// 認証セッションの refresh を自律的に維持する background service 実装。
    /// VContainer の <see cref="IAsyncStartable"/> として登録され、scope Awake 時に
    /// 周期 loop と reactive trigger subscription を開始する。
    /// </summary>
    public sealed class AuthSessionRefresher : IAuthSessionRefresher, IAsyncStartable
    {
        // ---- 定数 ----

        /// <summary>周期 check の interval。短めの pace で refresh 機会を複数持つ。</summary>
        private static readonly TimeSpan PeriodicCheckInterval = TimeSpan.FromMinutes(5);

        /// <summary>
        /// 周期 check で「refresh 必要」と判定する閾値。
        /// JWT 60 分 expiry に対する safety margin (約 10 分前)。
        /// </summary>
        private static readonly TimeSpan PeriodicRefreshThreshold = TimeSpan.FromMinutes(50);

        // ---- DI ----

        private readonly IAuthSessionService _session;
        private readonly IAuthApiService _authApi;
        private readonly INetworkService _network;
        private readonly IAppLifecycleSignals _lifecycle;
        private readonly IPublisher<SurvivorSignals.Auth.SessionRefreshResult> _resultPublisher;

        // ---- State ----

        private UniTaskCompletionSource<bool> _inFlight;
        private readonly object _lock = new();

        public bool IsRefreshing { get; private set; }

        public DateTime? LastRefreshAttemptAt { get; private set; }

        public RefreshTrigger LastRefreshTrigger { get; private set; }

        public int TotalRefreshCount { get; private set; }

        public int FailedRefreshCount { get; private set; }

        public AuthSessionRefresher(
            IAuthSessionService session,
            IAuthApiService authApi,
            INetworkService network,
            IAppLifecycleSignals lifecycle,
            IPublisher<SurvivorSignals.Auth.SessionRefreshResult> resultPublisher)
        {
            _session = session;
            _authApi = authApi;
            _network = network;
            _lifecycle = lifecycle;
            _resultPublisher = resultPublisher;
        }

        // ==============================================================
        // IAsyncStartable — VContainer が scope Awake で自動呼出
        // ct は scope dispose 時に自動 cancel されるため、_cts 自前作成は不要
        // ==============================================================
        public UniTask StartAsync(CancellationToken ct)
        {
            // Note: 初回 refresh は SurvivorGameRunner.TryRefreshSessionAsync() が担当する。
            // このメソッドでは proactive loop と reactive trigger subscription のみ開始する。
            RunPeriodicLoopAsync(ct).Forget();

            // Reactive triggers は R3 SubscribeAwait パターンで登録
            // - AwaitOperation.Sequential: event 連続発火時に並列 refresh を避ける
            // - RegisterTo(ct): scope dispose 時に自動 Dispose
            _network.OnConnectivityChanged
                .SubscribeAwait(async (connected, innerCt) =>
                {
                    if (!connected) return;
                    if (!_session.IsAuthenticated) return;
                    try
                    {
                        await EnsureFreshInternalAsync(RefreshTrigger.NetworkRecovery, innerCt);
                    }
                    catch (OperationCanceledException)
                    {
                        // scope cancel、正常終了
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[AuthSessionRefresher] NetworkRecovery refresh error: {e.Message}");
                    }
                }, AwaitOperation.Sequential)
                .RegisterTo(ct);

            _lifecycle.OnFocusChanged
                .SubscribeAwait(async (focused, innerCt) =>
                {
                    if (!focused) return;
                    if (!_session.IsAuthenticated) return;
                    if (!_network.IsConnected) return;
                    try
                    {
                        await EnsureFreshInternalAsync(RefreshTrigger.AppFocus, innerCt);
                    }
                    catch (OperationCanceledException)
                    {
                        // scope cancel、正常終了
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[AuthSessionRefresher] AppFocus refresh error: {e.Message}");
                    }
                }, AwaitOperation.Sequential)
                .RegisterTo(ct);

            return UniTask.CompletedTask;
        }

        // ==============================================================
        // Public API (scene/dialog から呼び出される)
        // ==============================================================
        public UniTask<bool> EnsureFreshAsync(CancellationToken ct = default)
            => EnsureFreshInternalAsync(RefreshTrigger.Explicit, ct);

        // ==============================================================
        // Internal: trigger 付きで refresh を実行
        // ==============================================================
        private async UniTask<bool> EnsureFreshInternalAsync(RefreshTrigger trigger, CancellationToken ct)
        {
            if (!_session.IsAuthenticated) return false;

            // Phase 1.5 の primitive を活用: 直近 refresh 済み (default 30 秒) なら skip (dedup)
            if (_session.IsRecentlyRefreshed()) return true;

            return await RefreshWithDedupAsync(trigger, ct);
        }

        // ==============================================================
        // Dedup: lock は「判定と代入のみ」に縮小
        // await は lock の外で実行 (lock + await anti-pattern 回避)
        // ==============================================================
        private async UniTask<bool> RefreshWithDedupAsync(RefreshTrigger trigger, CancellationToken ct)
        {
            UniTaskCompletionSource<bool> existing;
            UniTaskCompletionSource<bool> mine = null;

            lock (_lock)
            {
                existing = _inFlight;
                if (existing == null)
                {
                    mine = new UniTaskCompletionSource<bool>();
                    _inFlight = mine;
                }
            }

            if (existing != null)
            {
                // 既に進行中の refresh がある → その結果を待つ (lock 外で await)
                return await existing.Task.AttachExternalCancellation(ct);
            }

            // 自分が発火する側
            IsRefreshing = true;
            LastRefreshTrigger = trigger;
            LastRefreshAttemptAt = DateTime.UtcNow;
            TotalRefreshCount++;

            try
            {
                var response = await _authApi.RefreshTokenAsync();
                var result = new SurvivorSignals.Auth.SessionRefreshResult(
                    isSuccess: response.IsSuccess,
                    trigger: trigger,
                    errorMessage: response.IsSuccess ? null : response.Error?.Message);

                if (!response.IsSuccess) FailedRefreshCount++;

                TryPublishResult(result);
                mine.TrySetResult(response.IsSuccess);
                return response.IsSuccess;
            }
            catch (OperationCanceledException)
            {
                // Cancel は failure ではないので FailedRefreshCount を増やさない
                mine.TrySetCanceled();
                throw;
            }
            catch (Exception e)
            {
                FailedRefreshCount++;
                var result = new SurvivorSignals.Auth.SessionRefreshResult(
                    isSuccess: false, trigger: trigger, errorMessage: e.Message);
                TryPublishResult(result);
                mine.TrySetException(e);
                throw;
            }
            finally
            {
                IsRefreshing = false;
                lock (_lock) { _inFlight = null; }
            }
        }

        // ==============================================================
        // Periodic loop (5 分毎 check + 50 分 threshold)
        // IsRecentlyRefreshed(PeriodicRefreshThreshold) で「直近 refresh 済み」を skip
        // ==============================================================
        private async UniTaskVoid RunPeriodicLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await UniTask.Delay(PeriodicCheckInterval, DelayType.Realtime, cancellationToken: ct);

                    if (!_session.IsAuthenticated) continue;
                    if (!_network.IsConnected) continue;
                    if (_session.IsRecentlyRefreshed(PeriodicRefreshThreshold)) continue;

                    try
                    {
                        await RefreshWithDedupAsync(RefreshTrigger.Periodic, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        // scope cancel、loop 外の catch へ
                        throw;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[AuthSessionRefresher] Periodic refresh error: {e.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // scope dispose による正常終了
            }
        }

        // ==============================================================
        // MessagePipe publish は subscriber 例外を throw し得るので try-catch で保護
        // ==============================================================
        private void TryPublishResult(SurvivorSignals.Auth.SessionRefreshResult result)
        {
            try
            {
                _resultPublisher.Publish(result);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AuthSessionRefresher] Publish error: {e.Message}");
            }
        }
    }
}
