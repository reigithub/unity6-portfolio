using System;
using System.Threading.Tasks;
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

        public static Task<bool> RunAsync()
        {
            var sceneService = GameServiceManager.Instance.GetService<GameSceneService>();
            return sceneService.TransitionDialogAsync<GamePauseUIDialog, GamePauseUI, bool>(startup: (dialog, component) =>
            {
                component.Initialize(dialog);
                return Task.CompletedTask;
            });
        }

        public override Task Startup()
        {
            GlobalMessageBroker.GetAsyncPublisher<int, bool>().Publish(MessageKey.System.TimeScale, false);
            GlobalMessageBroker.GetAsyncPublisher<int, bool>().Publish(MessageKey.System.Cursor, true);
            return base.Startup();
        }

        public override Task Ready()
        {
            AudioService.PlayRandomOneAsync(AudioCategory.SoundEffect, AudioPlayTag.UIOpen).Forget();
            return base.Ready();
        }

        public override Task Terminate()
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

        private const float OnClickInterval = 3f; // Memo: ゲーム共通の定数としてもつか検討

        public void Initialize(GamePauseUIDialog dialog)
        {
            _resumeButton.OnClickAsObservable()
                .ThrottleFirst(TimeSpan.FromSeconds(OnClickInterval))
                .SubscribeAwait(async (_, token) =>
                {
                    SetInteractable(false);
                    await GlobalMessageBroker.GetAsyncPublisher<int, bool>().PublishAsync(MessageKey.GameStage.Resume, true, token);

                    // Memo: 一旦、どのメソッドでも閉じられる挙動にするという確認
                    dialog.TrySetResult(false);
                    // dialog.TrySetCanceled();
                    // dialog.Terminate();
                })
                .AddTo(this);
            _retryButton.OnClickAsObservable()
                .ThrottleFirst(TimeSpan.FromSeconds(OnClickInterval))
                .SubscribeAwait(async (_, token) =>
                {
                    SetInteractable(false);
                    await GlobalMessageBroker.GetAsyncPublisher<int, bool>().PublishAsync(MessageKey.GameStage.Retry, true, token);
                })
                .AddTo(this);
            _returnButton.OnClickAsObservable()
                .ThrottleFirst(TimeSpan.FromSeconds(OnClickInterval))
                .SubscribeAwait(async (_, token) =>
                {
                    SetInteractable(false);
                    await GlobalMessageBroker.GetAsyncPublisher<int, bool>().PublishAsync(MessageKey.GameStage.ReturnTitle, true, token);
                })
                .AddTo(this);
            _quitButton.OnClickAsObservable()
                .ThrottleFirst(TimeSpan.FromSeconds(OnClickInterval))
                .SubscribeAwait(async (_, token) =>
                {
                    SetInteractable(false);
                    await GlobalMessageBroker.GetAsyncPublisher<int, bool>().PublishAsync(MessageKey.Game.Quit, true, token);
                })
                .AddTo(this);

            SetInteractable(true);
        }

        private void SetInteractable(bool interactable)
        {
            _resumeButton.interactable = interactable;
            _retryButton.interactable = interactable;
            _returnButton.interactable = interactable;
            _quitButton.interactable = interactable;
        }
    }
}