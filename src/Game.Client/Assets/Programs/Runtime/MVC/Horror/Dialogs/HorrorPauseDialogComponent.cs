using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using Game.Shared.Bootstrap;
using R3;
using R3.Triggers;
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
        private InputSystemService InputService => _inputService ??= GameServiceManager.Get<InputSystemService>();

        public static async UniTask<PauseResult> RunAsync()
        {
            PauseResult result;
            var inputService = GameServiceManager.Get<InputSystemService>();
            inputService.ResolveControlScheme();
            using (inputService.BlockPlayer())
            {
                var sceneService = GameServiceManager.Get<GameSceneService>();
                result = await sceneService.TransitionDialogAsync<HorrorPauseDialog, PauseResult>();
            }
            inputService.ResolveControlScheme();
            return result;
        }

        public override UniTask PreInitialize()
        {
            ApplicationEvents.PauseTime();
            return base.PreInitialize();
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
                    Debug.Log($"{nameof(HorrorPauseDialog)}: OnOption");
                    // SceneComponent.SetInteractable(false);
                    await UniTask.Yield();
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
            if (Result != PauseResult.ReturnToTitle)
            {
                ApplicationEvents.ResumeTime();
            }

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
