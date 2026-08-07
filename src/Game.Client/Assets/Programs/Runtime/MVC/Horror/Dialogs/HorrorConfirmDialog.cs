using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using Game.Shared.Extensions;
using R3;

namespace Game.Horror.Dialogs
{
    public class HorrorConfirmDialog : GameDialogScene<HorrorConfirmDialog, HorrorConfirmDialogComponent, bool>
        , IGameSceneArg<string>
    {
        protected override string AssetPathOrAddress => "HorrorConfirmDialog";

        private readonly IInputSystemService _inputService = GameServiceManager.Resolve<IInputSystemService>();
        private string _messageLocalizeKey;

        public static async UniTask<bool> RunAsync(string messageLocalizeKey)
        {
            var sceneService = GameServiceManager.Resolve<IGameSceneService>();
            return await sceneService.TransitionDialogAsync<HorrorConfirmDialog, string, bool>(messageLocalizeKey, visibleLastScene: true);
        }

        public UniTask SetArg(string messageLocalizeKey)
        {
            _messageLocalizeKey = messageLocalizeKey;
            return UniTask.CompletedTask;
        }

        public override UniTask Startup()
        {
            _inputService.UI.Cancel.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing())
                .Subscribe(_ => TrySetResult(false))
                .AddTo(Disposables);

            SceneComponent.OnSubmit
                .Where(_ => State.IsProcessing())
                .Subscribe(x => TrySetResult(x))
                .AddTo(Disposables);

            SceneComponent.OnCancel
                .Where(_ => State.IsProcessing())
                .Subscribe(x => TrySetResult(x))
                .AddTo(Disposables);

            SceneComponent.Initialize(_messageLocalizeKey);

            return base.Startup();
        }
    }
}
