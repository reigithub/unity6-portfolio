using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.SaveData;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using Game.Shared.Bootstrap;
using Game.Shared.Extensions;
using R3;

namespace Game.Horror.Dialogs
{
    public class HorrorInventoryDialog : GameDialogScene<HorrorInventoryDialog, HorrorInventoryDialogComponent, bool>
    {
        protected override string AssetPathOrAddress => "HorrorInventoryDialog";

        private InputSystemService _inputService;

        public static async UniTask<bool> RunAsync()
        {
            var inputService = GameServiceManager.Get<InputSystemService>();
            bool result;
            using (inputService.BlockPlayer())
            using (inputService.BlockInputActions(inputService.UI.Menu))
            {
                var sceneService = GameServiceManager.Get<GameSceneService>();
                result = await sceneService.TransitionDialogAsync<HorrorInventoryDialog, bool>();
            }
            return result;
        }

        public override UniTask PreInitialize()
        {
            ApplicationEvents.PauseTime();
            _inputService = GameServiceManager.Get<InputSystemService>();
            return base.PreInitialize();
        }

        public override UniTask Startup()
        {
            // ダイアログキャンセル
            Observable.Merge(_inputService.UI.Cancel.OnPerformedAsObservable(), _inputService.UI.Inventory.OnPerformedAsObservable())
                .Where(_ => State.IsProcessing())
                .Subscribe(_ => TrySetResult(default))
                .AddTo(Disposables);

            // L1 (Previous) / R1 (Next) でタブ循環
            _inputService.UI.Previous.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing())
                .Subscribe(_ => SceneComponent.PreviousTab())
                .AddTo(Disposables);

            _inputService.UI.Next.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing())
                .Subscribe(_ => SceneComponent.NextTab())
                .AddTo(Disposables);

            // デバッグ用: 現在のインベントリを手動セーブ
            SceneComponent.OnSaveRequested
                .SubscribeAwait(async (_, _) => await GameServiceManager.Resolve<HorrorInventorySaveService>().SaveAsync())
                .AddTo(Disposables);

            SceneComponent.Initialize();


            return base.Startup();
        }

        public override async UniTask Terminate()
        {
            ApplicationEvents.ResumeTime();
            await base.Terminate();
        }
    }
}
