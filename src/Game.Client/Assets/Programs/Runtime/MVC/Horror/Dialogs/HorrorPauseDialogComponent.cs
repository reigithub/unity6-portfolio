using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using Game.Shared.Bootstrap;
using Game.Shared.Extensions;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Horror.Dialogs
{
    public enum PauseResult
    {
        Resume,
        ReturnToTitle,
        Quit
    }

    public class HorrorPauseDialog : GameDialogScene<HorrorPauseDialog, HorrorPauseDialogComponent, PauseResult>
    {
        protected override string AssetPathOrAddress => "HorrorPauseDialog";

        private InputSystemService _inputService;

        public static async UniTask<PauseResult> RunAsync()
        {
            PauseResult result;
            var inputService = GameServiceManager.Get<InputSystemService>();
            using (inputService.BlockPlayer())
            using (inputService.BlockInputActions(inputService.UI.Inventory))
            {
                var sceneService = GameServiceManager.Get<GameSceneService>();
                result = await sceneService.TransitionDialogAsync<HorrorPauseDialog, PauseResult>();
            }
            return result;
        }

        public override UniTask PreInitialize()
        {
            _inputService = GameServiceManager.Get<InputSystemService>();
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
                    TrySetResult(PauseResult.Resume);
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
                    TrySetResult(PauseResult.ReturnToTitle);
                })
                .AddTo(Disposables);
            SceneComponent.OnQuit
                .Subscribe(_ =>
                {
                    SceneComponent.SetInteractable(false);
                    TrySetResult(PauseResult.Quit);
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

    public class HorrorPauseDialogComponent : GameSceneComponent
    {
        [SerializeField]
        private Button _resumeButton;

        [SerializeField]
        private Button _optionButton;

        [SerializeField]
        private Button _returnButton;

        [SerializeField]
        private Button _quitButton;

        public Observable<Unit> OnResume => _resumeButton.OnClickAsObservable();
        public Observable<Unit> OnOption => _optionButton.OnClickAsObservable();
        public Observable<Unit> OnReturn => _returnButton.OnClickAsObservable();
        public Observable<Unit> OnQuit => _quitButton.OnClickAsObservable();
    }
}
