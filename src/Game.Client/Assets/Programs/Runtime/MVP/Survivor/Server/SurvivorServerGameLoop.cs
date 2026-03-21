using System.Threading;
using Cysharp.Threading.Tasks;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.SaveData;
using Game.MVP.Survivor.Scenes;
using Game.Shared.Network.Fusion;
using Game.Shared.Network.Survivor;
using Game.Shared.Services;
using Game.Shared.Signals.Survivor;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.MVP.Survivor.Server
{
    /// <summary>
    /// MPPM / Dedicated Server 用エントリポイント。
    /// AllPlayersReady シグナル受信後にマスターデータ読み込み → SurvivorNetworkStageScene へ遷移し、
    /// サーバー権威のウェーブ管理・エネミースポーンを開始する。
    /// クライアントのリトライ時は AllPlayersDisconnected → 次の AllPlayersReady で再遷移する。
    /// </summary>
    public class SurvivorServerGameLoop : IAsyncStartable
    {
        [Inject] private readonly IGameSceneService _sceneService;
        [Inject] private readonly ISurvivorSaveService _saveService;
        [Inject] private readonly IMasterDataService _masterDataService;
        [Inject] private readonly IFusionRunnerService _runnerService;
        [Inject] private readonly ISubscriber<SurvivorSignals.Session.AllPlayersReady> _allPlayersReadySub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Session.AllPlayersDisconnected> _allPlayersDisconnectedSub;

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            // マスターデータ読み込み（初回のみ）
            await _masterDataService.LoadMasterDataAsync();

            while (!cancellation.IsCancellationRequested)
            {
                Debug.Log("[SurvivorServerGameLoop] Waiting for AllPlayersReady...");

                // AllPlayersReady シグナルを待機
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

                Debug.Log("[SurvivorServerGameLoop] AllPlayersReady received, waiting for StageId...");

                // クライアントからのセッション情報を待機（タイムアウト付き）
                var stageId = 1;
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
                        Debug.LogWarning("[SurvivorServerGameLoop] Session info not received from client, using defaults");
                    }
                }
                Debug.Log($"[SurvivorServerGameLoop] Starting stage {stageId}, player {playerId}");

                _saveService.StartSession(stageId: stageId, playerId: playerId);

                // SurvivorNetworkStageScene へ遷移
                await _sceneService.TransitionAsync<SurvivorNetworkStageScene>();
                Debug.Log("[SurvivorServerGameLoop] SurvivorNetworkStageScene loaded on server");

                // 全プレイヤー離脱を待機（クライアントのリトライ/終了）
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

                // ステージシーンを終了して次のセッションに備える
                // （SurvivorNetworkStageScene.Terminate → 次のループで AllPlayersReady 待機）
            }
        }
    }
}
