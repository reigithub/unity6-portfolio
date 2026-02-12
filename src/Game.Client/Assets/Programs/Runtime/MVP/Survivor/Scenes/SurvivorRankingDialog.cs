using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Client.MasterData;
using Game.MVP.Core.Scenes;
using Game.Shared.Dto.Survivor;
using Game.Shared.Services;
using R3;
using VContainer;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// ランキングダイアログ（Presenter）
    /// ステージ別ランキングの表示と自分の順位確認
    /// </summary>
    public class SurvivorRankingDialog : GameDialogScene<SurvivorRankingDialog, SurvivorRankingDialogComponent, Unit>
    {
        protected override string AssetPathOrAddress => "SurvivorRankingDialog";

        [Inject] private readonly ISurvivorScoreApiService _scoreApiService;
        [Inject] private readonly ISessionService _sessionService;
        [Inject] private readonly IMasterDataService _masterDataService;
        [Inject] private readonly IInputService _inputService;

        private int _selectedStageId = 1;

        /// <summary>
        /// ダイアログを表示
        /// </summary>
        public static UniTask RunAsync(IGameSceneService sceneService)
        {
            return sceneService.TransitionDialogAsync<SurvivorRankingDialog, SurvivorRankingDialogComponent, Unit>();
        }

        public override async UniTask Startup()
        {
            await base.Startup();

            // ステージ選択肢を設定
            var stages = _masterDataService.MemoryDatabase.SurvivorStageMasterTable.All.ToList();
            SceneComponent.SetStageOptions(stages);

            // 最初のステージを選択
            if (stages.Count > 0)
            {
                _selectedStageId = stages[0].Id;
            }

            // Viewイベントを購読
            SceneComponent.OnStageSelected
                .Subscribe(stageId => OnStageSelected(stageId).Forget())
                .AddTo(Disposables);

            SceneComponent.OnCloseClicked
                .Subscribe(_ => OnClose().Forget())
                .AddTo(Disposables);

            SceneComponent.OnRefreshClicked
                .Subscribe(_ => OnRefreshClicked().Forget())
                .AddTo(Disposables);

            // 初期ランキングをロード
            await LoadRankingAsync(_selectedStageId);
        }

        public override async UniTask Ready()
        {
            // 入力受付フレームをずらす
            await UniTask.Yield();

            // Escapeキーで閉じる
            Observable.EveryValueChanged(_inputService, x => x.UI.Escape.WasPressedThisFrame(), UnityFrameProvider.Update)
                .Subscribe(escape =>
                {
                    if (escape) OnClose().Forget();
                })
                .AddTo(Disposables);
        }

        private async UniTask LoadRankingAsync(int stageId)
        {
            SceneComponent.ShowLoading(true);
            SceneComponent.ClearError();

            // ランキングを取得
            var rankingResponse = await _scoreApiService.GetRankingAsync(stageId);
            RankingEntry myRank = null;

            // 認証済みの場合は自分の順位も取得
            if (_sessionService.IsAuthenticated)
            {
                var myRankResponse = await _scoreApiService.GetMyRankAsync(stageId);
                if (myRankResponse.IsSuccess && myRankResponse.Data != null)
                {
                    myRank = myRankResponse.Data;
                }
            }

            SceneComponent.ShowLoading(false);

            if (rankingResponse.IsSuccess && rankingResponse.Data != null)
            {
                SceneComponent.SetRankingData(rankingResponse.Data, myRank);
            }
            else
            {
                var errorMessage = rankingResponse.Error?.Message ?? "Failed to load ranking";
                SceneComponent.ShowError(errorMessage);
            }
        }

        private async UniTaskVoid OnStageSelected(int stageId)
        {
            if (_selectedStageId == stageId)
            {
                return;
            }

            _selectedStageId = stageId;
            await LoadRankingAsync(stageId);
        }

        private async UniTaskVoid OnClose()
        {
            SceneComponent.SetInteractables(false);
            TrySetResult(Unit.Default);
            await UniTask.CompletedTask;
        }

        private async UniTaskVoid OnRefreshClicked()
        {
            await LoadRankingAsync(_selectedStageId);
        }
    }
}
