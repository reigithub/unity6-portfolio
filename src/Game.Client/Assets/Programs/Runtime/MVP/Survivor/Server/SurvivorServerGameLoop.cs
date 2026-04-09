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
    /// </summary>
    public class SurvivorServerGameLoop : IAsyncStartable
    {
        [Inject] private readonly IGameSceneService _sceneService;
        [Inject] private readonly ISurvivorSaveService _saveService;
        [Inject] private readonly IMasterDataService _masterDataService;
        [Inject] private readonly IFusionRunnerService _runnerService;
        [Inject] private readonly ISurvivorNetworkStageConnector _networkConnector;
        [Inject] private readonly ISubscriber<SurvivorSignals.Session.AllPlayersReady> _allPlayersReadySub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Session.AllPlayersDisconnected> _allPlayersDisconnectedSub;

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            // マスターデータ読み込み（初回のみ）
            await _masterDataService.LoadMasterDataAsync();

            while (!cancellation.IsCancellationRequested)
            {
                Debug.Log("[SurvivorServerGameLoop] Waiting for session start request via HTTP...");

                // Step 1: ServerHttpListener からのセッション作成リクエストを待機
                var request = await WaitForSessionRequestAsync(cancellation);

                Debug.Log($"[SurvivorServerGameLoop] Session request received: matchId={request.MatchId}, stageId={request.StageId}, players={request.ExpectedPlayers}");

                // Step 2: 接続パラメータを動的設定（リクエストの matchId を使用）
                SurvivorNetworkMatchConnector.ConfigureForDedicatedServer(
                    SurvivorNetworkMatchConnector.ServerPort,
                    SurvivorNetworkMatchConnector.ServerAddress,
                    request.MatchId);
                SurvivorNetworkMatchConnector.SetExpectedPlayerCount(request.ExpectedPlayers);

                // ServerHttpListener のステータスを active に更新
                UnityServerBootstrap.HttpListener?.SetSessionActive(request.MatchId);

                // Step 3: Fusion Server セッション開始
                await _networkConnector.StartServerAsync(request.StageId);

                // Step 4: HTTP レスポンス返却（セッション作成完了通知）
                request.CompletionSource.TrySetResult(true);

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
                    var timeout = System.TimeSpan.FromSeconds(5);
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

                // Step 7: Game.Server にセッション終了通知
                UnityServerBootstrap.NotifySessionEnded(request.MatchId);

                // ServerHttpListener のステータスを idle に戻す
                UnityServerBootstrap.HttpListener?.SetSessionIdle();

                // Fusion セッションのシャットダウン
                _networkConnector.Disconnect();

                Debug.Log("[SurvivorServerGameLoop] Session cleanup done, ready for next session");
            }
        }

        /// <summary>
        /// ServerHttpListener からセッション作成リクエストが届くまで待機する。
        /// 100ms 間隔でポーリングする。
        /// </summary>
        /// <param name="ct">キャンセルトークン。</param>
        /// <returns>デキューしたセッション作成リクエスト。</returns>
        private static async UniTask<ServerHttpListener.SessionStartRequest> WaitForSessionRequestAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var listener = UnityServerBootstrap.HttpListener;
                if (listener != null && listener.TryDequeueSessionRequest(out var request))
                    return request;

                await UniTask.Delay(100, cancellationToken: ct);
            }

            ct.ThrowIfCancellationRequested();
            return null; // 到達しない
        }
    }
}
