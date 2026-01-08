using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Core.Enums;
using Game.Core.Extensions;
using Game.Core.MessagePipe;
using Game.Core.Scenes;
using Game.Core.Services;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Contents.UI
{
    public class GamePauseUIDialog : GameDialogScene<GamePauseUIDialog, GamePauseUI, bool>
    {
        protected override string AssetPathOrAddress => "GamePauseUI";

        public static UniTask<bool> RunAsync()
        {
            var sceneService = GameServiceManager.Instance.GetService<GameSceneService>();
            return sceneService.TransitionDialogAsync<GamePauseUIDialog, GamePauseUI, bool>(
                initializer: (component, result) =>
                {
                    component.Initialize(result);
                    return UniTask.CompletedTask;
                });
        }

        public override UniTask Startup()
        {
            GlobalMessageBroker.GetAsyncPublisher<int, bool>().Publish(MessageKey.System.TimeScale, false);
            GlobalMessageBroker.GetAsyncPublisher<int, bool>().Publish(MessageKey.System.Cursor, true);
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
            GlobalMessageBroker.GetAsyncPublisher<int, bool>().Publish(MessageKey.System.TimeScale, true);
            GlobalMessageBroker.GetAsyncPublisher<int, bool>().Publish(MessageKey.System.Cursor, false);
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

        public void Initialize(IGameSceneResult<bool> result)
        {
            _resumeButton.OnClickAsObservableThrottleFirst()
                .SubscribeAwait(async (_, token) =>
                {
                    SetInteractiveAllButton(false);
                    await GlobalMessageBroker.GetAsyncPublisher<int, bool>().PublishAsync(MessageKey.GameStage.Resume, true, token);
                    result.TrySetResult(false);
                })
                .AddTo(this);
            _retryButton.OnClickAsObservableThrottleFirst()
                .SubscribeAwait(async (_, token) =>
                {
                    SetInteractiveAllButton(false);
                    await GlobalMessageBroker.GetAsyncPublisher<int, bool>().PublishAsync(MessageKey.GameStage.Retry, true, token);
                })
                .AddTo(this);
            _returnButton.OnClickAsObservableThrottleFirst()
                .SubscribeAwait(async (_, token) =>
                {
                    SetInteractiveAllButton(false);
                    await GlobalMessageBroker.GetAsyncPublisher<int, bool>().PublishAsync(MessageKey.GameStage.ReturnTitle, true, token);
                })
                .AddTo(this);
            _quitButton.OnClickAsObservableThrottleFirst()
                .SubscribeAwait(async (_, token) =>
                {
                    SetInteractiveAllButton(false);
                    await GlobalMessageBroker.GetAsyncPublisher<int, bool>().PublishAsync(MessageKey.Game.Quit, true, token);
                })
                .AddTo(this);

            SetInteractiveAllButton(true);
        }
    }
}