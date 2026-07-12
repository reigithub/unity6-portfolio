using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.Dialogs;
using Game.Horror.SaveData;
using Game.Horror.Services.Interfaces;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using Game.Shared.Bootstrap;
using Game.Shared.Extensions;
using R3;

namespace Game.Horror.Scenes
{
    public class HorrorTitleScene : GamePrefabScene<HorrorTitleScene, HorrorTitleSceneComponent>
    {
        protected override string AssetPathOrAddress => "HorrorTitleScene";

        private readonly IInputSystemService _inputService = GameServiceManager.Resolve<IInputSystemService>();
        private readonly IGameSceneService _sceneService = GameServiceManager.Resolve<IGameSceneService>();
        private readonly IHorrorSaveRepository _saveRepository = GameServiceManager.Resolve<IHorrorSaveRepository>();
        private IReadOnlyList<HorrorSaveSlotInfo> _saveSlots;
        private bool _hasSaveData;

        public override async UniTask Startup()
        {
            _saveSlots = await _saveRepository.LoadSlotInfosAsync();
            _hasSaveData = _saveSlots.Any(x => x.HasData);

            _inputService.UI.Cancel.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing())
                .Subscribe(_ => SceneComponent.CloseGameStartMenu())
                .AddTo(Disposables);

            SceneComponent.OnStart
                .Subscribe(_ => SceneComponent.OpenGameStartMenu())
                .AddTo(Disposables);

            SceneComponent.OnOption
                .SubscribeAwait(async (_, _) =>
                {
                    await HorrorOptionDialog.RunAsync();
                })
                .AddTo(Disposables);

            SceneComponent.OnReturn
                .SubscribeAwait(async (_, _) =>
                {
                    SceneComponent.SetInteractable(false);
                    await _sceneService.TerminateLastAsync();
                    await ApplicationEvents.RequestReturnToTitleAsync();
                })
                .AddTo(Disposables);

            SceneComponent.OnQuit
                .Subscribe(_ =>
                {
                    SceneComponent.SetInteractable(false);
                    ApplicationEvents.RequestShutdown();
                })
                .AddTo(Disposables);

            SceneComponent.OnContinueGame
                .SubscribeAwait(async (_, _) =>
                {
                    if (!_hasSaveData) return;

                    // TODO: オートセーブスロットを含む最新のデータから開始
                    var slotInfo = _saveSlots
                        .Where(x => x.HasData)
                        .OrderByDescending(x => x.SavedAtUtc)
                        .FirstOrDefault();
                    if (slotInfo != null)
                    {
                        int slotNo = slotInfo.SlotNo;
                        if (slotNo > 0)
                        {
                            await _saveRepository.LoadBySlotAsync(slotNo);
                            await _sceneService.TransitionAsync<HorrorStageScene>();
                        }
                    }
                })
                .AddTo(Disposables);

            SceneComponent.OnLoadGame
                .SubscribeAwait(async (_, _) =>
                {
                    if (!_hasSaveData) return;

                    var slotNo = await HorrorSaveDataDialog.RunAsync(_saveSlots);
                    if (slotNo > 0)
                    {
                        await _saveRepository.LoadBySlotAsync(slotNo);
                        await _sceneService.TransitionAsync<HorrorStageScene>();
                    }
                })
                .AddTo(Disposables);

            SceneComponent.OnNewGame
                .SubscribeAwait(async (_, _) =>
                {
                    if (_hasSaveData) return;
                    _saveRepository.CreateData();
                    await _sceneService.TransitionAsync<HorrorStageScene>();
                })
                .AddTo(Disposables);

            SceneComponent.Initialize(_hasSaveData);

            await base.Startup();
        }
    }
}
