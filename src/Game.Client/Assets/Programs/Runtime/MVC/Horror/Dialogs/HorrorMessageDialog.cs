using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.Services.Interfaces;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using Game.Shared.Bootstrap;
using Game.Shared.Extensions;
using R3;

namespace Game.Horror.Dialogs
{
    public class HorrorMessageDialog : GameDialogScene<HorrorMessageDialog, HorrorMessageDialogComponent, bool>
        , IGameSceneArg<string>
    {
        protected override string AssetPathOrAddress => "HorrorMessageDialog";

        private readonly IInputSystemService _inputService = GameServiceManager.Resolve<IInputSystemService>();
        private readonly IHorrorUISoundService _uiSoundService = GameServiceManager.Resolve<IHorrorUISoundService>();
        private string _message;

        public static async UniTask<bool> RunAsync(string message, bool visible = true)
        {
            bool result;
            var inputService = GameServiceManager.Resolve<IInputSystemService>();
            using (inputService.BlockPlayer())
            {
                var sceneService = GameServiceManager.Resolve<IGameSceneService>();
                result = await sceneService.TransitionDialogAsync<HorrorMessageDialog, string, bool>(message, visible);
            }
            return result;
        }


        public UniTask SetArg(string message)
        {
            _message =  message;
            return UniTask.CompletedTask;
        }

        public override UniTask PreInitialize()
        {
            ApplicationEvents.PauseTime();
            return base.PreInitialize();
        }

        public override UniTask Startup()
        {
            _inputService.UI.Submit.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing())
                .Subscribe(_ => TrySetResult(true))
                .AddTo(Disposables);

            _inputService.UI.Cancel.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing())
                .Subscribe(_ =>
                {
                    _uiSoundService.PlayCancelSfx();
                    TrySetResult(false);
                })
                .AddTo(Disposables);

            SceneComponent.SetMessage(_message);

            return base.Startup();
        }

        public override UniTask Terminate()
        {
            ApplicationEvents.ResumeTime();
            return base.Terminate();
        }
    }
}
