using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using Game.Shared.Bootstrap;
using Game.Shared.Extensions;
using R3;

namespace Game.Horror.Dialogs
{
    public enum HorrorPauseResult
    {
        Resume,
        ReturnToTitle,
        Quit
    }

    public class HorrorPauseDialog : GameDialogScene<HorrorPauseDialog, HorrorPauseDialogComponent, HorrorPauseResult>
    {
        protected override string AssetPathOrAddress => "HorrorPauseDialog";

        private readonly IInputSystemService _inputService = GameServiceManager.Resolve<IInputSystemService>();

        public static async UniTask<HorrorPauseResult> RunAsync()
        {
            HorrorPauseResult result;
            var inputService = GameServiceManager.Resolve<IInputSystemService>();
            using (inputService.BlockPlayer())
            using (inputService.BlockInputActions(inputService.UI.Inventory))
            {
                var sceneService = GameServiceManager.Resolve<IGameSceneService>();
                result = await sceneService.TransitionDialogAsync<HorrorPauseDialog, HorrorPauseResult>();
            }
            return result;
        }

        public override UniTask PreInitialize()
        {
            ApplicationEvents.PauseTime();
            return base.PreInitialize();
        }

        public override UniTask Startup()
        {
            Observable.Merge(_inputService.UI.Cancel.OnPerformedAsObservable(), _inputService.UI.Menu.OnPerformedAsObservable())
                .Where(_ => State.IsProcessing())
                .Subscribe(_ => TrySetResult(default))
                .AddTo(Disposables);

            SceneComponent.OnResume
                .Subscribe(_ =>
                {
                    SceneComponent.SetInteractable(false);
                    TrySetResult(HorrorPauseResult.Resume);
                })
                .AddTo(Disposables);
            SceneComponent.OnOption
                .SubscribeAwait(async (_, _) =>
                {
                    await HorrorOptionDialog.RunAsync();
                })
                .AddTo(Disposables);
            SceneComponent.OnReturn
                .Subscribe(_ =>
                {
                    SceneComponent.SetInteractable(false);
                    TrySetResult(HorrorPauseResult.ReturnToTitle);
                })
                .AddTo(Disposables);
            SceneComponent.OnQuit
                .Subscribe(_ =>
                {
                    SceneComponent.SetInteractable(false);
                    TrySetResult(HorrorPauseResult.Quit);
                })
                .AddTo(Disposables);

            return base.Startup();
        }

        public override UniTask Terminate()
        {
            ApplicationEvents.ResumeTime();

            return base.Terminate();
        }
    }
}
