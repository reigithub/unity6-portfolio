using Cysharp.Threading.Tasks;
using Game.MVP.Core.Scenes;
using Game.Shared.Services;
using R3;
using VContainer;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// ポーズダイアログの結果
    /// </summary>
    public enum SurvivorPauseResult
    {
        Resume,
        Retry,
        Quit
    }

    /// <summary>
    /// Survivorポーズダイアログ（Presenter）
    /// MVPパターンでViewとの仲介を行う
    /// </summary>
    public class SurvivorPauseDialog : GameDialogScene<SurvivorPauseDialog, SurvivorPauseDialogComponent, SurvivorPauseResult>
    {
        protected override string AssetPathOrAddress => "SurvivorPauseDialog";

        [Inject] private readonly IInputService _inputService;
        [Inject] private readonly IGameSceneService _sceneService;

        public static UniTask<SurvivorPauseResult> RunAsync(IGameSceneService sceneService)
        {
            return sceneService.TransitionDialogAsync<SurvivorPauseDialog, SurvivorPauseDialogComponent, SurvivorPauseResult>();
        }

        public override UniTask Startup()
        {
            // Viewのイベントを購読
            SceneComponent.OnResultSelected
                .Subscribe(OnResultSelected)
                .AddTo(Disposables);

            SceneComponent.OnOptionsClicked
                .Subscribe(_ => OnOptionsClicked().Forget())
                .AddTo(Disposables);

            return base.Startup();
        }

        public override async UniTask Ready()
        {
            // 入力受付フレームをずらす
            await UniTask.Yield();

            Observable.EveryValueChanged(_inputService, x => x.UI.Escape.WasPressedThisFrame(), UnityFrameProvider.Update)
                .Subscribe(escape =>
                {
                    if (escape) OnResultSelected(SurvivorPauseResult.Resume);
                })
                .AddTo(Disposables);
        }

        private void OnResultSelected(SurvivorPauseResult result)
        {
            SceneComponent.SetInteractables(false);
            TrySetResult(result);
        }

        private async UniTaskVoid OnOptionsClicked()
        {
            // ポーズダイアログを開いたままオプションダイアログを表示
            SceneComponent.SetInteractables(false);
            await SurvivorOptionsDialog.RunAsync(_sceneService);
            SceneComponent.SetInteractables(true);
        }
    }
}