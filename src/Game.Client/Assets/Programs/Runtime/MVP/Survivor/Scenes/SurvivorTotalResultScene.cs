using System;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.Enums;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.SaveData;
using Game.MVP.Survivor.Scenes.ViewModels;
using Game.Shared.Network.Survivor;
using Game.Shared.Realtime.Client;
using Game.Shared.Services;
using Game.Shared.Services.Network;
using Game.Shared.Services.Network.Queue;
using R3;
using VContainer;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// Survivor総合リザルトシーン（Presenter）
    /// 全ステージの結果を表示し、ゲームセッションを終了する
    /// </summary>
    public class SurvivorTotalResultScene : GamePrefabScene<SurvivorTotalResultScene, SurvivorTotalResultSceneComponent>, IGameSceneArg<bool>
    {
        [Inject] private readonly IGameSceneService _sceneService;
        [Inject] private readonly IAudioService _audioService;
        [Inject] private readonly ISurvivorSaveService _saveService;
        [Inject] private readonly ISurvivorScoreApiService _scoreApiService;
        [Inject] private readonly IAuthSessionService _authSessionService;
        [Inject] private readonly IAuthSessionRefresher _authSessionRefresher;
        [Inject] private readonly INetworkService _networkService;
        [Inject] private readonly IQueueNotificationService _queueNotificationService;
        [Inject] private readonly IRequestQueue _requestQueue;
        [Inject] private readonly ISurvivorNetworkStageConnector _networkConnector;
        [Inject] private readonly ILobbyClient _lobbyClient;

        private readonly TotalResultSceneViewModel _viewModel = new();

        protected override string AssetPathOrAddress => "SurvivorTotalResultScene";

        private bool _isVictory;
        private bool _isMultiPlayer;

        public UniTask ArgHandle(bool isMultiPlayer)
        {
            _isMultiPlayer = isMultiPlayer;
            return UniTask.CompletedTask;
        }

        public override async UniTask Startup()
        {
            await base.Startup();

            var session = _saveService.CurrentSession;
            if (session == null)
            {
                UnityEngine.Debug.LogError("[SurvivorTotalResultScene] No session found!");
                return;
            }

            _isVictory = _viewModel.IsOverallVictory(session);

            // 認証済みの場合のみスコア送信
            if (_authSessionService.IsAuthenticated)
            {
                await SubmitScoresAsync(session);
            }

            // リザルトデータをViewに反映
            SceneComponent.SetResultData(
                totalScore: session.TotalGroupScore,
                totalKills: session.TotalGroupKills,
                stageResults: session.StageResults,
                isVictory: _isVictory
            );

            // P2P (Host / Client) も Matchmaking と同じくマルチプレイヤー扱いにし、
            // RETURN TO LOBBY ボタン表示でロビーに戻れるようにする。
            SceneComponent.SetDisplayButtons(_isMultiPlayer);

            // Viewイベントを購読 (SP 用ボタン)
            SceneComponent.OnRetryClicked
                .Subscribe(_ => OnRetry().Forget())
                .AddTo(Disposables);

            SceneComponent.OnStageSelectClicked
                .Subscribe(_ => OnStageSelect().Forget())
                .AddTo(Disposables);

            SceneComponent.OnReturnToTitleClicked
                .Subscribe(_ => OnReturnToTitle().Forget())
                .AddTo(Disposables);

            SceneComponent.OnReturnToLobbyClicked
                .Subscribe(_ => OnReturnToLobby().Forget())
                .AddTo(Disposables);

            // キュー通知を購読
            _queueNotificationService.OnPendingCountChanged
                .Subscribe(count => SceneComponent.UpdateQueueStatus(count))
                .AddTo(Disposables);

            _queueNotificationService.OnNotification
                .Where(n => n.Type == QueueNotificationType.ProcessingCompleted)
                .Subscribe(_ => SceneComponent.ShowQueueProcessingComplete())
                .AddTo(Disposables);
        }

        public override async UniTask Ready()
        {
            if (_isVictory)
            {
                SceneComponent.PlayAnimation("Salute");
                await _audioService.PlayRandomOneAsync(AudioPlayTag.StageClear);
            }
            else
            {
                SceneComponent.PlayAnimation("KneelDown");
                await _audioService.PlayRandomOneAsync(AudioPlayTag.StageFailed);
            }
        }

        /// <summary>
        /// サーバーにスコアを送信
        /// オンライン時は即座に送信、オフラインまたは失敗時はキューに追加
        /// </summary>
        private async UniTask SubmitScoresAsync(SurvivorStageSession session)
        {
            SceneComponent.ShowScoreSubmissionStatus(NetworkErrorLocalizer.GetScoreSubmittingMessage());

            // Scenario B 対策: Title 経由せず長時間プレイした場合の JWT 期限切れ防御
            // refresher が IsRecentlyRefreshed() で skip 判定するので、fresh 状態なら no-op
            if (_networkService.IsConnected)
            {
                await _authSessionRefresher.EnsureFreshAsync();
            }

            foreach (var result in session.StageResults)
            {
                try
                {
                    var request = _viewModel.BuildScoreRequest(result, session.CurrentWave);

                    // オンライン時は即座に送信を試行
                    if (_networkService.IsConnected)
                    {
                        var response = await _scoreApiService.SubmitScoreAsync(request);
                        if (response.IsSuccess)
                        {
                            // 送信成功 → ランキングキャッシュを無効化（次回表示時に最新を取得）
                            await _scoreApiService.InvalidateRankingCacheAsync(result.StageId);

                            if (response.Data?.IsNewBest == true)
                            {
                                SceneComponent.ShowNewBestEffect(result.StageId, response.Data.CurrentRank);
                            }
                            continue;
                        }

                        UnityEngine.Debug.LogWarning(
                            $"[SurvivorTotalResultScene] Score submit failed: HTTP {response.StatusCode}, error={response.Error?.Error}, message={response.Error?.Message}");
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning("[SurvivorTotalResultScene] Score submit skipped: network offline");
                    }

                    // オフラインまたは失敗時はキューに追加
                    await _scoreApiService.EnqueueSubmitScoreAsync(request);
                    SceneComponent.ShowScoreQueuedNotice(result.StageId);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError(
                        $"[SurvivorTotalResultScene] Failed to submit score for stage {result.StageId}: {ex.Message}");
                    SceneComponent.ShowScoreQueuedNotice(result.StageId);
                }
            }

            SceneComponent.HideScoreSubmissionStatus();

            // SurvivorGameRunner.SetupQueueProcessing は再接続時のみ発火するため、
            // 接続維持中に失敗したリクエストをここで即時処理する（_processLock で重複ガード済）。
            if (_networkService.IsConnected && _requestQueue.PendingCount > 0)
            {
                _requestQueue.ProcessQueueAsync().Forget();
            }
        }

        private async UniTaskVoid OnRetry()
        {
            SceneComponent.SetInteractables(false);

            // 同じステージで新規セッション開始
            var session = _saveService.CurrentSession;
            var stageId = session?.StageId ?? 1;
            var playerId = session?.PlayerId ?? _saveService.Data.SelectedPlayerId;

            _saveService.EndSession();
            _saveService.StartSession(stageId, playerId);
            await _saveService.SaveIfDirtyAsync();

            await _sceneService.TransitionAsync<SurvivorStageConnectScene>();
        }

        private async UniTaskVoid OnStageSelect()
        {
            SceneComponent.SetInteractables(false);

            // ゲームセッション終了
            _saveService.EndSession();
            await _saveService.SaveIfDirtyAsync();

            await _sceneService.TransitionAsync<SurvivorStageSelectScene>();
        }

        private async UniTaskVoid OnReturnToTitle()
        {
            SceneComponent.SetInteractables(false);

            // ゲームセッション終了
            _saveService.EndSession();
            await _saveService.SaveIfDirtyAsync();

            await _sceneService.TransitionAsync<SurvivorTitleScene>();
        }

        /// <summary>
        /// MP 用: Fusion DS 接続を切断してロビーに戻る。
        /// 直前に参加していたロビーが存続していれば LobbyRoomScene へ直接戻り、
        /// そうでなければロビーリスト (LobbyScene) にフォールバックする。
        /// </summary>
        private async UniTaskVoid OnReturnToLobby()
        {
            SceneComponent.SetInteractables(false);

            // ゲームセッション終了
            _saveService.EndSession();
            await _saveService.SaveIfDirtyAsync();

            // Fusion DS 接続を明示的に切断（Runner.Shutdown + IFusionRunnerService.Clear）
            try
            {
                await _networkConnector.DisconnectAsync();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning(
                    $"[SurvivorTotalResultScene] Failed to disconnect Fusion runner: {ex.Message}");
            }

            // ロビー存続確認 (server-side: GetMyLobbyAsync は NotFound 時 null を返す実装)
            Game.Library.Shared.Dto.LobbyInfo lobby = null;
            try
            {
                lobby = await _lobbyClient.GetMyLobbyAsync();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning(
                    $"[SurvivorTotalResultScene] GetMyLobby failed: {ex.Message}");
            }

            if (lobby != null && !string.IsNullOrEmpty(lobby.LobbyId))
            {
                // ロビー存続。StreamingHub が切断されていれば再接続してから LobbyRoomScene へ
                if (string.IsNullOrEmpty(_lobbyClient.CurrentLobbyId))
                {
                    try
                    {
                        var playerName = _authSessionService.UserName ?? "Player";
                        await _lobbyClient.ConnectToLobbyAsync(lobby.LobbyId, playerName);
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogWarning(
                            $"[SurvivorTotalResultScene] Failed to reconnect lobby hub: {ex.Message}. Falling back to lobby list.");
                        await _sceneService.TransitionAsync<SurvivorLobbyScene>();
                        return;
                    }
                }

                await _sceneService.TransitionAsync<SurvivorLobbyRoomScene>();
                return;
            }

            // ロビー不在 (閉鎖済み or 自分が外された) → ロビーリストへフォールバック
            await _sceneService.TransitionAsync<SurvivorLobbyScene>();
        }
    }
}
