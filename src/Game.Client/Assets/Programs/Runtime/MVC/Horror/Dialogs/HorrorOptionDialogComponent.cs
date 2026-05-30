using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using R3;
using R3.Triggers;

namespace Game.Horror.Dialogs
{
    public class HorrorOptionDialog : GameDialogScene<HorrorOptionDialog, HorrorOptionDialogComponent, bool>
    {
        protected override string AssetPathOrAddress => "HorrorOptionDialog";

        private InputSystemService _inputService;
        private InputSystemService InputService => _inputService ??= GameServiceManager.Get<InputSystemService>();

        public static async UniTask<bool> RunAsync()
        {
            var sceneService = GameServiceManager.Get<GameSceneService>();
            return await sceneService.TransitionDialogAsync<HorrorOptionDialog, bool>();
        }

        public override UniTask Startup()
        {
            SceneComponent.UpdateAsObservable()
                .Subscribe(_ =>
                {
                    if (FocusState is GameSceneFocusState.Unfocused)
                        return;

                    if (InputService.UI.Cancel.WasPressedThisFrame() || InputService.UI.Menu.WasPressedThisFrame())
                    {
                        TrySetResult(default);
                    }
                })
                .AddTo(Disposables);

            return base.Startup();
        }

        public override UniTask Terminate()
        {

            return base.Terminate();
        }
    }

    public class HorrorOptionDialogComponent : GameSceneComponent
    {
    }
}
