using System;
using Cysharp.Threading.Tasks;
using Game.Shared.Extensions;
using Game.Core.Services;
using Game.Library.Shared.Enums;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using Game.Shared.Bootstrap;
using Game.Shared.Services;
using R3;
using R3.Triggers;
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

        private IAudioService _audioService;
        private IInputSystemService _inputService;

        public static async UniTask<PauseDialogResult> RunAsync()
        {
            PauseDialogResult result;
            var inputService = GameServiceManager.Resolve<IInputSystemService>();
            using (inputService.BlockPlayer())
            {
                var sceneService = GameServiceManager.Resolve<IGameSceneService>();
                result = await sceneService.TransitionDialogAsync<GamePauseUIDialog, PauseDialogResult>();
            }
            return result;
        }

        public override UniTask PreInitialize()
        {
            _audioService = GameServiceManager.Resolve<IAudioService>();
            _inputService = GameServiceManager.Resolve<IInputSystemService>();
            return base.PreInitialize();
        }

        public override UniTask Startup()
        {
            ApplicationEvents.PauseTime();
            SceneComponent.Initialize(this);
            SceneComponent.UpdateAsObservable()
                .Where(_ => State.IsProcessing())
                .Subscribe(_ =>
                {
                    if (_inputService.UI.Cancel.WasPressedThisFrame() || _inputService.UI.Menu.WasPressedThisFrame())
                    {
                        TrySetResult(default);
                    }
                })
                .AddTo(Disposables);
            return base.Startup();
        }

        public override UniTask Ready()
        {
            _audioService.PlayRandomOneAsync(AudioCategory.SoundEffect, AudioPlayTag.UIOpen).Forget();
            return base.Ready();
        }

        public override UniTask Terminate()
        {
            _audioService.PlayRandomOneAsync(AudioCategory.SoundEffect, AudioPlayTag.UIClose).Forget();

            if (Result != PauseDialogResult.ReturnToTitle)
            {
                ApplicationEvents.ResumeTime();
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
                    SetInteractable(false);
                    result.TrySetResult(PauseDialogResult.Resume);
                })
                .AddTo(Disposables);
            _retryButton.OnClickAsObservableThrottleFirst()
                .Subscribe(_ =>
                {
                    SetInteractable(false);
                    result.TrySetResult(PauseDialogResult.Retry);
                })
                .AddTo(Disposables);
            _returnButton.OnClickAsObservableThrottleFirst()
                .Subscribe(_ =>
                {
                    SetInteractable(false);
                    result.TrySetResult(PauseDialogResult.ReturnToTitle);
                })
                .AddTo(Disposables);
            _quitButton.OnClickAsObservableThrottleFirst()
                .Subscribe(_ =>
                {
                    SetInteractable(false);
                    result.TrySetResult(PauseDialogResult.Quit);
                })
                .AddTo(Disposables);

            SetInteractable(true);
        }
    }
}
