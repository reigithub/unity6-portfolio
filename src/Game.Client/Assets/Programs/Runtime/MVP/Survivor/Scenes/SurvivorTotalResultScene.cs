using System.Linq;
using Cysharp.Threading.Tasks;
using Game.MVP.Core.Scenes;
using Game.MVP.Core.Services;
using Game.MVP.Survivor.SaveData;
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
        [Inject] private readonly ISurvivorSaveService _saveService;

        protected override string AssetPathOrAddress => "SurvivorTotalResultScene";

        // ReSharper disable Unity.PerformanceAnalysis
        public override async UniTask Startup()
        {
            await base.Startup();

            var session = _saveService.CurrentSession;
            if (session == null)
            {
                UnityEngine.Debug.LogError("[SurvivorTotalResultScene] No session found!");
                return;
            }

            // リザルトデータをViewに反映
            SceneComponent.SetResultData(
                totalScore: session.TotalGroupScore,
                totalKills: session.TotalGroupKills,
                stageResults: session.StageResults,
                isVictory: IsOverallVictory(session)
            );

            // Viewイベントを購読
            SceneComponent.OnRetryClicked
                .Subscribe(_ => OnRetry().Forget())
                .AddTo(Disposables);

            SceneComponent.OnReturnToTitleClicked
                .Subscribe(_ => OnReturnToTitle().Forget())
                .AddTo(Disposables);
        }

        private bool IsOverallVictory(StageSession session)
        {
            if (session.StageResults.Count == 0) return false;
            return session.StageResults.All(r => r.IsVictory);
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