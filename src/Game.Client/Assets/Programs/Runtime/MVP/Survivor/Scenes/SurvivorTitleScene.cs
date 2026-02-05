using Cysharp.Threading.Tasks;
using Game.Library.Shared.Enums;
using Game.MVP.Core.Scenes;
using Game.Shared.Bootstrap;
using Game.Shared.Services;
using R3;
using VContainer;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// Survivorタイトルシーン（Presenter）
    /// MVPパターンでViewとの仲介を行う
    /// </summary>
    public class SurvivorTitleScene : GamePrefabScene<SurvivorTitleScene, SurvivorTitleSceneComponent>
    {
        [Inject] private readonly IGameSceneService _sceneService;
        [Inject] private readonly IAudioService _audioService;
        [Inject] private readonly ISessionService _sessionService;
        [Inject] private readonly IAuthApiService _authApiService;

        protected override string AssetPathOrAddress => "SurvivorTitleScene";

        public override async UniTask Startup()
        {
            await base.Startup();

            // Viewのイベントを購読
            SceneComponent.OnStartGameClicked
                .Subscribe(_ => OnStartGame().Forget())
                .AddTo(Disposables);

            SceneComponent.OnReturnClicked
                .Subscribe(_ => OnReturn().Forget())
                .AddTo(Disposables);

            SceneComponent.OnQuitClicked
                .Subscribe(_ => OnQuit().Forget())
                .AddTo(Disposables);

            SceneComponent.OnOptionsClicked
                .Subscribe(_ => OnOptions().Forget())
                .AddTo(Disposables);

            SceneComponent.OnDataLinkClicked
                .Subscribe(_ => OnDataLink().Forget())
                .AddTo(Disposables);
        }

        public override async UniTask Ready()
        {
            SceneComponent.PlayAnimation();
            await _audioService.PlayRandomOneAsync(AudioPlayTag.GameReady);
        }

        private async UniTaskVoid OnStartGame()
        {
            SceneComponent.SetInteractables(false);
            await _audioService.PlayRandomOneAsync(AudioPlayTag.GameStart);

            if (!_sessionService.IsAuthenticated)
            {
                var result = await _authApiService.GuestLoginAsync();
                if (!result.IsSuccess)
                {
                    SceneComponent.SetInteractables(true);
                    return;
                }
            }

            await _sceneService.TransitionAsync<SurvivorStageSelectScene>();
        }

        private async UniTaskVoid OnReturn()
        {
            SceneComponent.SetInteractables(false);
            await _audioService.PlayRandomOneAsync(AudioPlayTag.GameQuit);
            await ApplicationEvents.RequestReturnToTitleAsync();
        }

        private async UniTaskVoid OnQuit()
        {
            SceneComponent.SetInteractables(false);
            await _audioService.PlayRandomOneAsync(AudioPlayTag.GameQuit);
            ApplicationEvents.RequestShutdown();
        }

        private async UniTaskVoid OnOptions()
        {
            SceneComponent.SetInteractables(false);
            await SurvivorOptionsDialog.RunAsync(_sceneService);
            SceneComponent.SetInteractables(true);
        }

        private async UniTaskVoid OnDataLink()
        {
            SceneComponent.SetInteractables(false);
            await SurvivorAccountLinkDialog.RunAsync(_sceneService);
            SceneComponent.SetInteractables(true);
        }
    }
}
