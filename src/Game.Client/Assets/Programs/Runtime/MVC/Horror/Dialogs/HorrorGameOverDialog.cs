using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.SaveData;
using Game.Horror.Scenes;
using Game.Horror.Services.Interfaces;
using Game.MVC.Core.Scenes;
using Game.Shared.Bootstrap;
using R3;
using UnityEngine.InputSystem;

namespace Game.Horror.Dialogs
{
    public class HorrorGameOverDialog : GameDialogScene<HorrorGameOverDialog, HorrorGameOverDialogComponent, bool>
    {
        protected override string AssetPathOrAddress => "HorrorGameOverDialog";

        private readonly IGameSceneService _sceneService = GameServiceManager.Resolve<IGameSceneService>();
        private readonly IHorrorSaveRepository _saveRepository = GameServiceManager.Resolve<IHorrorSaveRepository>();
        private HorrorSaveSlotInfo[] _saveSlots;

        public static async UniTask<bool> RunAsync()
        {
            bool result;
            var inputService = GameServiceManager.Resolve<IInputSystemService>();
            using (inputService.BlockPlayer())
            using (inputService.BlockInputAction(inputService.UI.Menu))
            using (inputService.BlockInputAction(inputService.UI.Inventory))
            {
                var sceneService = GameServiceManager.Resolve<IGameSceneService>();
                result = await sceneService.TransitionDialogAsync<HorrorGameOverDialog, bool>();
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

            SceneComponent.OnContinueGame
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
            SceneComponent.OnQuit
                .SubscribeAwait(async (_, _) =>
                {
                    SceneComponent.SetInteractable(false);
                    await _sceneService.TransitionAsync<HorrorTitleScene>();
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
