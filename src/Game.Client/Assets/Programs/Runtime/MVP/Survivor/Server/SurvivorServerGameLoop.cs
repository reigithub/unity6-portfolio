using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.SaveData;
using Game.MVP.Survivor.Scenes;
using Game.Shared.Network.Fusion;
using Game.Shared.Network.Survivor;
using Game.Shared.Services;
using Game.Shared.Signals.Survivor;
using Game.Shared.Unity.Server;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.MVP.Survivor.Server
{
    /// <summary>
    /// MPPM / Dedicated Server 用エントリポイント。
    /// ServerHttpListener からの /session/start リクエストを待機し、
    /// 接続パラメータを動的設定してから Fusion Server セッションを開始する。
    /// AllPlayersDisconnected 後はセッション終了通知を送信し、次のリクエスト待機に戻る。
    /// 単一セッション内の例外はループ外へ伝播させず、クリーンアップを保証してから次のリクエストを受け付ける。
    /// </summary>
    public class SurvivorServerGameLoop : IAsyncStartable
    {
        [Inject] private readonly IGameSceneService _sceneService;
        [Inject] private readonly ISurvivorSaveService _saveService;
        [Inject] private readonly IMasterDataService _masterDataService;
        [Inject] private readonly IFusionRunnerService _runnerService;
        [Inject] private readonly ISurvivorNetworkStageConnector _networkConnector;
        [Inject] private readonly IUnityServerSessionConfig _sessionConfig;
        [Inject] private readonly IUnityServerHttpListener _listener;
        [Inject] private readonly IUnityServerRegistryApiClient _registry;
        [Inject] private readonly UnityServerBootstrap _bootstrap;
        [Inject] private readonly ISubscriber<SurvivorSignals.Session.AllPlayersReady> _allPlayersReadySub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Session.AllPlayersDisconnected> _allPlayersDisconnectedSub;

        /// <summary>
        /// サーバーメインループを開始する。
        /// UnityServerBootstrap の起動完了を待ってから処理を開始する。
        /// セッション単位の例外境界を try/catch/finally で構成し、
        /// どの例外でも CompletionSource 応答・Fusion シャットダウン・Listener idle・
        /// NotifySessionEnded が漏れなく実行されることを保証する。
        /// </summary>
        /// <param name="cancellation">キャンセルトークン。</param>
        public async UniTask StartAsync(CancellationToken cancellation)
        {
            // 起動バリア: UnityServerBootstrap.StartAsync 完了（Listener 起動完了）を待つ
            await _bootstrap.WaitForStartupAsync(cancellation);

            // マスターデータ読み込み（初回のみ）
            await _masterDataService.LoadMasterDataAsync();

            while (!cancellation.IsCancellationRequested)
            {
                Debug.Log("[SurvivorServerGameLoop] Waiting for session start request via HTTP...");

                // Step 1: ServerHttpListener からのセッション作成リクエストを待機
                // try の外に置き、cancellation 時はループを抜ける（正常終了経路）
                var request = await WaitForSessionRequestAsync(cancellation);

                Debug.Log($"[SurvivorServerGameLoop] Session request received: sessionName={request.SessionName}, stageId={request.StageId}, players={request.PlayerCount}");

                // 事前バリデーション（try の外・Fusion/Listener 未操作）: DS 自身のマスターに stageId が存在するか
                // 不正な stageId は Fusion セッションを作らずに即座に拒否し、次のリクエスト待機に戻る
                // try の外に置くことで、この経路では SafeCleanupAsync（Fusion shutdown 等）が呼ばれない
                if (!_masterDataService.MemoryDatabase.SurvivorStageMasterTable.TryFindById(request.StageId, out _))
                {
                    Debug.LogWarning($"[SurvivorServerGameLoop] Unknown stageId rejected: {request.StageId}");
                    request.CompletionSource.TrySetResult(false);
                    continue;
                }

                // セッション単位の例外境界
                // sessionAcceptedByServer: CompletionSource に true を返した後かを追跡する
                var sessionAcceptedByServer = false;
                try
                {
                    // Step 2: 接続パラメータを動的設定（sessionName / playerCount / hostUserId）
                    // hostUserId は DS Server 側で「手動ポーズ操作の権限を持つ Client」の判定に使用。
                    _sessionConfig.UpdateConfigure(
                        sessionName: request.SessionName,
                        playerCount: request.PlayerCount,
                        hostUserId: request.HostUserId);

                    // ServerHttpListener のステータスを active に更新
                    _listener.SetSessionActive(request.SessionName);

                    // Step 3: Fusion Server セッション開始
                    await _networkConnector.StartServerAsync(request.StageId);

                    // Step 4: HTTP レスポンス返却（セッション作成完了通知）
                    // ここ以降の失敗は Game.Server 側 Valkey の巻き戻し責務を持つ
                    request.CompletionSource.TrySetResult(true);
                    sessionAcceptedByServer = true;

                    Debug.Log("[SurvivorServerGameLoop] Fusion session started, waiting for AllPlayersReady...");

                    // Step 5: AllPlayersReady シグナルを待機（既存フロー）
                    var readyTcs = new UniTaskCompletionSource();
                    var readySub = _allPlayersReadySub.Subscribe(_ => readyTcs.TrySetResult());
                    try
                    {
                        await readyTcs.Task;
                    }
                    finally
                    {
                        readySub.Dispose();
                    }

                    Debug.Log("[SurvivorServerGameLoop] AllPlayersReady received, starting stage...");

                    // クライアントからのセッション情報を待機（タイムアウト付き）
                    var stageId = request.StageId;
                    var playerId = 1;
                    if (_runnerService.TryGet<SurvivorFusionGameState>(out var gameState))
                    {
                        var timeout = TimeSpan.FromSeconds(5);
                        await UniTask.WhenAny(
                            UniTask.WaitUntil(() => gameState.StageId > 0, cancellationToken: cancellation),
                            UniTask.Delay(timeout, DelayType.Realtime, cancellationToken: cancellation));

                        if (gameState.StageId > 0) stageId = gameState.StageId;
                        if (gameState.PlayerId > 0) playerId = gameState.PlayerId;

                        if (gameState.StageId <= 0)
                        {
                            Debug.LogWarning("[SurvivorServerGameLoop] Session info not received from client, using HTTP request defaults");
                        }
                    }

                    Debug.Log($"[SurvivorServerGameLoop] Starting stage {stageId}, player {playerId}");
                    _saveService.StartSession(stageId: stageId, playerId: playerId);

                    // SurvivorNetworkStageScene へ遷移
                    await _sceneService.TransitionAsync<SurvivorNetworkStageScene>();
                    Debug.Log("[SurvivorServerGameLoop] SurvivorNetworkStageScene loaded on server");

                    // Step 6: 全プレイヤー離脱を待機
                    var disconnectTcs = new UniTaskCompletionSource();
                    var disconnectSub = _allPlayersDisconnectedSub.Subscribe(_ => disconnectTcs.TrySetResult());
                    try
                    {
                        await disconnectTcs.Task;
                    }
                    finally
                    {
                        disconnectSub.Dispose();
                    }

                    Debug.Log("[SurvivorServerGameLoop] All players disconnected, resetting for next session");
                }
                catch (OperationCanceledException)
                {
                    // 正常なシャットダウン経路。上位に伝播させてループを抜ける
                    throw;
                }
                catch (Exception ex)
                {
                    // セッション内で予期しない例外が発生した場合はログを出して次のセッションへ
                    Debug.LogError($"[SurvivorServerGameLoop] Session aborted: {ex}");

                    // CompletionSource がまだ未完了（Step 4 より前で失敗）の場合は失敗応答を返す
                    if (!sessionAcceptedByServer)
                    {
                        request.CompletionSource.TrySetResult(false);
                    }
                }
                finally
                {
                    // try に入った時点（Step 2 以降）に到達していれば、
                    // Fusion/Listener に対する操作の巻き戻しが必要。
                    // 事前バリデーション失敗経路は try 外の continue で抜けるのでここには来ない。
                    await SafeCleanupAsync(request.SessionName, sessionAcceptedByServer);
                }

                Debug.Log("[SurvivorServerGameLoop] Session cleanup done, ready for next session");
            }
        }

        /// <summary>
        /// セッション終了時のクリーンアップを安全に実行する。
        /// 各ステップが独立した try/catch で囲まれており、1 つの失敗が次のステップを阻害しない。
        /// 順序: Fusion shutdown → Listener idle → (wasAccepted なら) NotifySessionEnded
        /// </summary>
        /// <param name="sessionName">終了した Fusion セッション名（SessionName）。</param>
        /// <param name="wasAccepted">CompletionSource に true を返した（Step 4 以降）か。</param>
        private async UniTask SafeCleanupAsync(string sessionName, bool wasAccepted)
        {
            // 1. Fusion shutdown（Photon Cloud 上のセッションを確実に消す。最優先）
            try
            {
                await _networkConnector.DisconnectAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Cleanup] DisconnectAsync failed: {ex}");
            }

            // 2. ローカル Listener を idle に戻す
            try
            {
                _listener.SetSessionIdle();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Cleanup] SetSessionIdle failed: {ex}");
            }

            // 3. Game.Server にセッション終了通知
            // Step 4 より前の失敗なら Game.Server 側は未だ active 化していないので通知不要
            // cleanup は cancellation されてはいけないので CancellationToken.None を渡す
            if (wasAccepted)
            {
                try
                {
                    await _registry.NotifySessionEndedAsync(sessionName, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Cleanup] NotifySessionEndedAsync failed: {ex}");
                }
            }
        }

        /// <summary>
        /// ServerHttpListener からセッション作成リクエストが届くまで待機する。
        /// 100ms 間隔でポーリングする。
        /// </summary>
        /// <param name="ct">キャンセルトークン。</param>
        /// <returns>デキューしたセッション作成リクエスト。</returns>
        private async UniTask<UnityServerSessionRequest> WaitForSessionRequestAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (_listener.TryDequeueSessionRequest(out var request))
                    return request;

                await UniTask.Delay(100, cancellationToken: ct);
            }

            ct.ThrowIfCancellationRequested();
            return null; // 到達しない
        }
    }
}
