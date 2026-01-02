using System.Threading.Tasks;
using Game.Core.MessagePipe;
using Game.Core.Scenes;
using Game.Core.Services;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Contents.UI
{
    public class GamePauseUIDialog : GameDialogScene<GamePauseUIDialog, GamePauseUI, bool>
    {
        protected override string AssetPathOrAddress => "Assets/Prefabs/UI/GamePauseUI.prefab";

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

        public override Task Terminate()
        {
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

        public void Initialize(GamePauseUIDialog dialog)
        {
            _resumeButton.onClick.AddListener(() =>
            {
                // Memo: 一旦、どのメソッドでも閉じられる挙動にするという確認
                dialog.TrySetResult(false);
                // dialog.TrySetCanceled();
                // dialog.Terminate();
            });
            _retryButton.onClick.AddListener(() => { GlobalMessageBroker.GetAsyncPublisher<int, bool>().Publish(MessageKey.GameStage.Retry, true); });
            _returnButton.onClick.AddListener(() => { GlobalMessageBroker.GetAsyncPublisher<int, bool>().Publish(MessageKey.GameStage.ReturnTitle, true); });
            _quitButton.onClick.AddListener(() => { GlobalMessageBroker.GetPublisher<int, bool>().Publish(MessageKey.Game.Quit, true); });
        }
    }
}