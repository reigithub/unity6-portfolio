using Cysharp.Threading.Tasks;
using Game.MVP.Core.Scenes;
using Game.Shared.Services;
using R3;
using VContainer;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// 確認ダイアログのオプション
    /// </summary>
    public class SurvivorConfirmDialogOptions
    {
        public string Title { get; set; } = "CONFIRM";
        public string Message { get; set; } = "Are you sure?";
        public string ConfirmButtonText { get; set; } = "OK";
        public string CancelButtonText { get; set; } = "CANCEL";
    }

    /// <summary>
    /// 確認ダイアログ（Presenter）
    /// 汎用的な確認ダイアログとして使用可能
    /// </summary>
    public class SurvivorConfirmDialog :
        GameDialogScene<SurvivorConfirmDialog, SurvivorConfirmDialogComponent, bool>,
        IGameSceneArg<SurvivorConfirmDialogOptions>
    {
        protected override string AssetPathOrAddress => "SurvivorConfirmDialog";

        [Inject] private readonly IInputService _inputService;

        private SurvivorConfirmDialogOptions _options;

        /// <summary>
        /// ダイアログを表示して結果を取得
        /// </summary>
        public static UniTask<bool> RunAsync(IGameSceneService sceneService, SurvivorConfirmDialogOptions options = null)
        {
            options ??= new SurvivorConfirmDialogOptions();
            return sceneService.TransitionDialogAsync<SurvivorConfirmDialog, SurvivorConfirmDialogComponent, SurvivorConfirmDialogOptions, bool>(options);
        }

        public UniTask ArgHandle(SurvivorConfirmDialogOptions arg)
        {
            _options = arg;
            return UniTask.CompletedTask;
        }

        public override UniTask Startup()
        {
            // オプションを適用
            if (_options != null)
            {
                SceneComponent.SetTitle(_options.Title);
                SceneComponent.SetMessage(_options.Message);
                SceneComponent.SetConfirmButtonText(_options.ConfirmButtonText);
                SceneComponent.SetCancelButtonText(_options.CancelButtonText);
            }

            // Viewのイベントを購読
            SceneComponent.OnResultSelected
                .Subscribe(x => OnResultSelected(x))
                .AddTo(Disposables);

            return base.Startup();
        }

        public override async UniTask Ready()
        {
            // 入力受付フレームをずらす
            await UniTask.Yield();

            // Escapeキーでキャンセル
            Observable.EveryValueChanged(_inputService, x => x.UI.Escape.WasPressedThisFrame(), UnityFrameProvider.Update)
                .Subscribe(escape =>
                {
                    if (escape) OnResultSelected(false);
                })
                .AddTo(Disposables);
        }

        private void OnResultSelected(bool result)
        {
            SceneComponent.SetInteractables(false);
            TrySetResult(result);
        }
    }
}
