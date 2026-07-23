using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.SaveData;
using Game.Horror.Scenes;
using Game.Horror.Services.Interfaces;
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
        private readonly IGameSceneService _sceneService = GameServiceManager.Resolve<IGameSceneService>();
        private readonly IHorrorSaveRepository _saveRepository = GameServiceManager.Resolve<IHorrorSaveRepository>();
        private HorrorSaveSlotInfo[] _saveSlots;

        public static async UniTask<HorrorPauseResult> RunAsync()
        {
            HorrorPauseResult result;
            var inputService = GameServiceManager.Resolve<IInputSystemService>();
            using (inputService.BlockPlayer(inputService.Player.Menu))
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

        public override async UniTask Startup()
        {
            _saveSlots = await _saveRepository.LoadSlotInfosAsync();

            Observable.Merge(_inputService.UI.Cancel.OnPerformedAsObservable(), _inputService.Player.Menu.OnPerformedAsObservable())
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
            SceneComponent.OnRestart
                .SubscribeAwait(async (_, _) =>
                {
                    if (_saveSlots.Any(x => x.HasData))
                        await _saveRepository.LoadByCurrentSlotAsync();
                    else
                        _saveRepository.CreateData();

                    await _sceneService.TransitionAsync<HorrorStageScene>();
                })
                .AddTo(Disposables);
            SceneComponent.OnLoadGame
                .SubscribeAwait(async (_, _) =>
                {
                    var slotNo = await HorrorSaveDataDialog.RunAsync(_saveSlots, saveMode: false);
                    if (slotNo >= 0)
                    {
                        await _saveRepository.LoadBySlotAsync(slotNo);
                        await _sceneService.TransitionAsync<HorrorStageScene>();
                    }
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

            await base.Startup();
        }

        public override UniTask Terminate()
        {
            ApplicationEvents.ResumeTime();

            return base.Terminate();
        }
    }
}
