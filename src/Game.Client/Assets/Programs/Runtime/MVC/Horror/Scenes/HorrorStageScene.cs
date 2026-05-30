using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.Dialogs;
using Game.Horror.Player;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using Game.Shared.Bootstrap;
using Game.Shared.Scenes;
using R3;
using R3.Triggers;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Game.Horror.Scenes
{
    public class HorrorStageScene : GamePrefabScene<HorrorStageScene, HorrorStageSceneComponent>
    {
        protected override string AssetPathOrAddress => "HorrorStageScene";

        private GameSceneService _sceneService;
        private GameSceneService SceneService => _sceneService ??= GameServiceManager.Get<GameSceneService>();

        private InputSystemService _inputService;
        private InputSystemService InputService => _inputService ??= GameServiceManager.Get<InputSystemService>();

        private SceneInstance _stageSceneInstance;

        public override async UniTask Startup()
        {
            await LoadUnitySceneAsync();
            await LoadPlayerAsync();
            SubscribeEvents();
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
        }

        private async UniTask LoadPlayerAsync()
        {
            var playerStart = GameSceneHelper.GetComponentInChildren<HorrorPlayerStart>(_stageSceneInstance.Scene);
            await playerStart.LoadPlayerAsync();
        }

        private void SubscribeEvents()
        {
            SceneComponent
                .UpdateAsObservable()
                .Subscribe(_ =>
                {
                    if (FocusState is GameSceneFocusState.Unfocused)
                        return;

                    if (InputService.UI.Menu.WasPressedThisFrame())
                    {
                        ShowPauseDialogAsync().Forget();
                    }
                })
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
                    await SceneService.TransitionAsync<HorrorTitleScene>();
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
