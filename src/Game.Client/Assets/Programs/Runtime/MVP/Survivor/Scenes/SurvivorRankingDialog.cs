using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Client.MasterData;
using Game.MVP.Core.Scenes;
using Game.Library.Shared.Dto;
using Game.Shared.Services;
using Game.Shared.Services.Network;
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
        [Inject] private readonly IAuthSessionService _authSessionService;
        [Inject] private readonly IMasterDataService _masterDataService;
        [Inject] private readonly IInputService _inputService;
        [Inject] private readonly INetworkService _networkService;  // UI表示用のみ

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
            SceneComponent.HideCacheNotice();

            // ランキング取得（キャッシュ対応はSurvivorScoreApiService経由）
            var response = await _scoreApiService.GetRankingAsync(stageId);

            RankingEntryDto myRank = null;

            // 認証済みの場合は自分の順位も取得
            if (_authSessionService.IsAuthenticated && response.IsSuccess)
            {
                var myRankResponse = await _scoreApiService.GetMyRankAsync(stageId);
                if (myRankResponse.IsSuccess && myRankResponse.Data != null)
                {
                    myRank = myRankResponse.Data;
                }
            }

            SceneComponent.ShowLoading(false);

            if (response.IsSuccess)
            {
                SceneComponent.SetRankingData(response.Data, myRank);

                // キャッシュからのデータの場合は通知を表示
                if (response.FromCache)
                {
                    SceneComponent.ShowCacheNotice(NetworkErrorLocalizer.GetCacheNoticeMessage());
                }
            }
            else if (response.Error?.Error == "Offline")
            {
                SceneComponent.ShowError(NetworkErrorLocalizer.GetOfflineMessage());
            }
            else
            {
                SceneComponent.ShowError(NetworkErrorLocalizer.GetLocalizedMessage(response.Error));
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
