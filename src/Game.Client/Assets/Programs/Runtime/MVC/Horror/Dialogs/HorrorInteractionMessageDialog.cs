using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using Game.Shared.Bootstrap;
using Game.Shared.Extensions;
using Game.Shared.Localization;
using R3;

namespace Game.Horror.Dialogs
{
    public class HorrorInteractionMessageDialog : GameDialogScene<HorrorInteractionMessageDialog, HorrorInteractionMessageDialogComponent, bool>
        , IGameSceneArg<string>
    {
        protected override string AssetPathOrAddress => "HorrorInteractionMessageDialog";

        private InputSystemService _inputService;
        private string _messageKey;

        public static async UniTask<bool> RunAsync(string messageKey)
        {
            if (string.IsNullOrEmpty(messageKey)) return false;

            bool result;
            var inputService = GameServiceManager.Get<InputSystemService>();
            using (inputService.BlockPlayer())
            using (inputService.BlockInputActions(inputService.UI.Menu, inputService.UI.Inventory))
            {
                var sceneService = GameServiceManager.Get<GameSceneService>();
                result = await sceneService.TransitionDialogAsync<HorrorInteractionMessageDialog, string, bool>(messageKey);
            }
            return result;
        }


        public UniTask SetArg(string messageKey)
        {
            _messageKey =  messageKey;
            return UniTask.CompletedTask;
        }

        public override UniTask PreInitialize()
        {
            _inputService = GameServiceManager.Get<InputSystemService>();
            ApplicationEvents.PauseTime();
            return base.PreInitialize();
        }

        public override UniTask Startup()
        {
            _inputService.UI.Cancel.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing())
                .Subscribe(_ => TrySetResult(default))
                .AddTo(Disposables);

            SceneComponent.OnClose
                .Subscribe(_ =>
                {
                    SceneComponent.SetInteractable(false);
                    TrySetResult(true);
                })
                .AddTo(Disposables);

            var message = InteractionMessagesLocalizer.Localize(_messageKey);
            SceneComponent.SetMessage(message);

            return base.Startup();
        }

        public override UniTask Terminate()
        {
            ApplicationEvents.ResumeTime();
            return base.Terminate();
        }
    }
}
