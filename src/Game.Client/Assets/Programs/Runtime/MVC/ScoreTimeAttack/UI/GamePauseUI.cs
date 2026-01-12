using Cysharp.Threading.Tasks;
using Game.Core.Extensions;
using Game.Core.MessagePipe;
using Game.Core.Services;
using Game.Library.Shared.Enums;
using Game.MVC.Core.Scenes;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Contents.UI
{
    public class GamePauseUIDialog : GameDialogScene<GamePauseUIDialog, GamePauseUI, bool>
    {
        protected override string AssetPathOrAddress => "GamePauseUI";

        private AudioService _audioService;
        private AudioService AudioService => _audioService ??= GameServiceManager.Get<AudioService>();

        private MessagePipeService _messagePipeService;
        private MessagePipeService MessagePipeService => _messagePipeService ??= GameServiceManager.Get<MessagePipeService>();

        public static UniTask<bool> RunAsync()
        {
            var sceneService = GameServiceManager.Get<GameSceneService>();
            return sceneService.TransitionDialogAsync<GamePauseUIDialog, GamePauseUI, bool>(
                initializer: (component, result) =>
                {
                    component.Initialize(result);
                    return UniTask.CompletedTask;
                });
        }

        public override UniTask Startup()
        {
            MessagePipeService.PublishForget(MessageKey.System.TimeScale, false);
            MessagePipeService.PublishForget(MessageKey.System.Cursor, true);
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
            MessagePipeService.PublishForget(MessageKey.System.TimeScale, true);
            MessagePipeService.PublishForget(MessageKey.System.Cursor, false);
            return base.Terminate();
        }
    }

    public class GamePauseUI : GameSceneComponent
    {
        private MessagePipeService _messagePipeService;
        private MessagePipeService MessagePipeService => _messagePipeService ??= GameServiceManager.Get<MessagePipeService>();

        [SerializeField]
        private Button _resumeButton;

        [SerializeField]
        private Button _retryButton;

        [SerializeField]
        private Button _returnButton;

        [SerializeField]
        private Button _quitButton;

        public void Initialize(IGameSceneResult<bool> result)
        {
            _resumeButton.OnClickAsObservableThrottleFirst()
                .SubscribeAwait(async (_, token) =>
                {
                    SetInteractiveAllButton(false);
                    await MessagePipeService.PublishAsync(MessageKey.GameStage.Resume, true, token);
                    result.TrySetResult(false);
                })
                .AddTo(this);
            _retryButton.OnClickAsObservableThrottleFirst()
                .SubscribeAwait(async (_, token) =>
                {
                    SetInteractiveAllButton(false);
                    await MessagePipeService.PublishAsync(MessageKey.GameStage.Retry, true, token);
                })
                .AddTo(this);
            _returnButton.OnClickAsObservableThrottleFirst()
                .SubscribeAwait(async (_, token) =>
                {
                    SetInteractiveAllButton(false);
                    await MessagePipeService.PublishAsync(MessageKey.GameStage.ReturnTitle, true, token);
                })
                .AddTo(this);
            _quitButton.OnClickAsObservableThrottleFirst()
                .SubscribeAwait(async (_, token) =>
                {
                    SetInteractiveAllButton(false);
                    await MessagePipeService.PublishAsync(MessageKey.Game.Quit, true, token);
                })
                .AddTo(this);

            SetInteractiveAllButton(true);
        }
    }
}