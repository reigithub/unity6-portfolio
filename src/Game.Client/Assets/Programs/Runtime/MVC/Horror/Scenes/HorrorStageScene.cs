using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.Dialogs;
using Game.Horror.Player;
using Game.Horror.SaveData;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using Game.Shared.Bootstrap;
using Game.Shared.Extensions;
using Game.Shared.Scenes;
using R3;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Game.Horror.Scenes
{
    public class HorrorStageScene : GamePrefabScene<HorrorStageScene, HorrorStageSceneComponent>
    {
        protected override string AssetPathOrAddress => "HorrorStageScene";

        private GameSceneService _sceneService;
        private InputSystemService _inputService;
        private HorrorOptionSaveService _optionSaveService;

        private SceneInstance _stageSceneInstance;

        public override UniTask PreInitialize()
        {
            _sceneService = GameServiceManager.Get<GameSceneService>();
            _inputService = GameServiceManager.Get<InputSystemService>();
            _optionSaveService = GameServiceManager.Resolve<HorrorOptionSaveService>();
            return base.PreInitialize();
        }

        public override async UniTask Startup()
        {
            await LoadUnitySceneAsync();
            await LoadPlayerAsync();

            _inputService.UI.Menu.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing())
                .SubscribeAwait(async (_, _) => await ShowPauseDialogAsync())
                .AddTo(Disposables);

            await base.Startup();
        }

        public override UniTask Ready()
        {
            ApplicationEvents.ResumeTime();
            return base.Ready();
        }

        public override async UniTask Terminate()
        {
            await UnloadUnitySceneAsync();
            await base.Terminate();
        }

        private async UniTask LoadUnitySceneAsync()
        {
            Physics.simulationMode = SimulationMode.FixedUpdate;
            _stageSceneInstance = await AssetService.LoadSceneAsync("Abandoned_Asylum");
            SceneManager.SetActiveScene(_stageSceneInstance.Scene);
        }

        private async UniTask UnloadUnitySceneAsync()
        {
            await AssetService.UnloadSceneAsync(_stageSceneInstance);
            _stageSceneInstance = default;
        }

        private async UniTask LoadPlayerAsync()
        {
            var playerStart = GameSceneHelper.GetComponentInChildren<HorrorPlayerStart>(_stageSceneInstance.Scene);
            var player = await playerStart.LoadPlayerAsync();
            player.Initialize(_optionSaveService.Data);
            _optionSaveService.OnSaved
                .Subscribe(data => player.ApplyOptions(data))
                .AddTo(Disposables);
        }

        private async UniTask ShowPauseDialogAsync()
        {
            var result = await HorrorPauseDialog.RunAsync();
            switch (result)
            {
                case PauseResult.Resume:
                {
                    break;
                }
                case PauseResult.ReturnToTitle:
                {
                    await _sceneService.TransitionAsync<HorrorTitleScene>();
                    break;
                }
                case PauseResult.Quit:
                {
                    ApplicationEvents.RequestShutdown();
                    break;
                }
            }
        }
    }
}
