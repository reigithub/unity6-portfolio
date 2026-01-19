using Cysharp.Threading.Tasks;
using Game.Shared.Extensions;
using Game.Core.Services;
using Game.Library.Shared.Enums;
using Game.MVC.Core.Scenes;
using Game.Shared.Bootstrap;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.ScoreTimeAttack.UI
{
    public enum PauseDialogResult
    {
        Resume,
        Retry,
        ReturnToTitle,
        Quit
    }

    public class GamePauseUIDialog : GameDialogScene<GamePauseUIDialog, GamePauseUI, PauseDialogResult>
    {
        protected override string AssetPathOrAddress => "GamePauseUI";

        private AudioService _audioService;
        private AudioService AudioService => _audioService ??= GameServiceManager.Get<AudioService>();

        public static UniTask<PauseDialogResult> RunAsync()
        {
            var sceneService = GameServiceManager.Get<GameSceneService>();
            return sceneService.TransitionDialogAsync<GamePauseUIDialog, GamePauseUI, PauseDialogResult>(
                initializer: (component, result) =>
                {
                    component.Initialize(result);
                    return UniTask.CompletedTask;
                });
        }

        public override UniTask Startup()
        {
            ApplicationEvents.PauseTime();
            ApplicationEvents.ShowCursor();
            return base.Startup();
        }

        public override UniTask Ready()
        {
            AudioService.PlayRandomOneAsync(AudioCategory.SoundEffect, AudioPlayTag.UIOpen).Forget();
            return base.Ready();
        }

        public override UniTask Terminate()
        {
            AudioService.PlayRandomOneAsync(AudioCategory.SoundEffect, AudioPlayTag.UIClose).Forget();

            if (Result != PauseDialogResult.ReturnToTitle)
            {
                ApplicationEvents.ResumeTime();
                ApplicationEvents.HideCursor();
            }

            return base.Terminate();
        }
    }

    public class GamePauseUI : GameSceneComponent
    {
        [SerializeField]
        private Button _resumeButton;

        [SerializeField]
        private Button _retryButton;

        [SerializeField]
        private Button _returnButton;

        [SerializeField]
        private Button _quitButton;

        public void Initialize(IGameSceneResult<PauseDialogResult> result)
        {
            _resumeButton.OnClickAsObservableThrottleFirst()
                .Subscribe(_ =>
                {
                    SetInteractables(false);
                    result.TrySetResult(PauseDialogResult.Resume);
                })
                .AddTo(this);
            _retryButton.OnClickAsObservableThrottleFirst()
                .Subscribe(_ =>
                {
                    SetInteractables(false);
                    result.TrySetResult(PauseDialogResult.Retry);
                })
                .AddTo(this);
            _returnButton.OnClickAsObservableThrottleFirst()
                .Subscribe(_ =>
                {
                    SetInteractables(false);
                    result.TrySetResult(PauseDialogResult.ReturnToTitle);
                })
                .AddTo(this);
            _quitButton.OnClickAsObservableThrottleFirst()
                .Subscribe(_ =>
                {
                    SetInteractables(false);
                    result.TrySetResult(PauseDialogResult.Quit);
                })
                .AddTo(this);

            SetInteractables(true);
        }
    }
}