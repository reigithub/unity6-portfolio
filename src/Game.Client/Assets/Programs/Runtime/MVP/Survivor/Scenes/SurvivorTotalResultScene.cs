using System;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.Enums;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.SaveData;
using Game.MVP.Survivor.Scenes.ViewModels;
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
    public class SurvivorTotalResultScene : GamePrefabScene<SurvivorTotalResultScene, SurvivorTotalResultSceneComponent>
    {
        [Inject] private readonly IGameSceneService _sceneService;
        [Inject] private readonly IAudioService _audioService;
        [Inject] private readonly ISurvivorSaveService _saveService;
        [Inject] private readonly ISurvivorScoreApiService _scoreApiService;
        [Inject] private readonly IAuthSessionService _authSessionService;
        [Inject] private readonly INetworkService _networkService;
        [Inject] private readonly IQueueNotificationService _queueNotificationService;

        private readonly TotalResultSceneViewModel _viewModel = new();

        protected override string AssetPathOrAddress => "SurvivorTotalResultScene";

        private bool _isVictory;

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

            // Viewイベントを購読
            SceneComponent.OnRetryClicked
                .Subscribe(_ => OnRetry().Forget())
                .AddTo(Disposables);

            SceneComponent.OnStageSelectClicked
                .Subscribe(_ => OnStageSelect().Forget())
                .AddTo(Disposables);

            SceneComponent.OnReturnToTitleClicked
                .Subscribe(_ => OnReturnToTitle().Forget())
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
                            if (response.Data?.IsNewBest == true)
                            {
                                SceneComponent.ShowNewBestEffect(result.StageId, response.Data.CurrentRank);
                            }
                            continue;
                        }
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

            await _sceneService.TransitionAsync<SurvivorStageScene>();
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
    }
}