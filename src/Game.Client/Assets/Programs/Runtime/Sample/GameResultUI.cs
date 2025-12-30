using System.Threading.Tasks;
using Game.Contents.Scenes;
using Game.Core.MessagePipe;
using Game.Core.Scenes;
using Game.Core.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sample
{
    public class GameResultUIDialog : GameDialogScene<GameResultUIDialog, GameResultUI, bool>
    {
        protected override string AssetPathOrAddress => "Assets/Prefabs/UI/GameResultUI.prefab";

        public static Task<bool> RunAsync(GameStageResultData data)
        {
            var sceneService = GameServiceManager.Instance.GetService<GameSceneService>();
            return sceneService.TransitionDialogAsync<GameResultUIDialog, GameResultUI, bool>(startup: (dialog, component) =>
            {
                component.Initialize(dialog, data);
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
            return base.Terminate();
        }
    }

    public class GameResultUI : GameSceneComponent
    {
        [SerializeField]
        private TextMeshProUGUI _result;

        [SerializeField]
        private TextMeshProUGUI _point;

        [SerializeField]
        private TextMeshProUGUI _maxPoint;

        [SerializeField]
        private TextMeshProUGUI _hp;

        [SerializeField]
        private TextMeshProUGUI _maxHp;

        [SerializeField]
        private Button _nextButton;

        [SerializeField]
        private Button _returnButton;

        public void Initialize(GameResultUIDialog dialog, GameStageResultData data)
        {
            _result.text = data.StageResult == GameStageResult.Clear
                ? "Clear!"
                : "Failed...";

            _point.text = data.Point.ToString();
            _maxPoint.text = data.MaxPoint.ToString();

            _hp.text = data.PlayerHp.ToString();
            _maxHp.text = data.PlayerMaxHp.ToString();

            _nextButton.onClick.AddListener(() =>
            {
                dialog.TrySetResult(true);
                // TODO: 次のステージへ遷移
                GlobalMessageBroker.GetAsyncPublisher<int, bool>().Publish(MessageKey.GameStage.ReturnTitle, true);
            });
            _returnButton.onClick.AddListener(() =>
            {
                dialog.TrySetResult(false);
                GlobalMessageBroker.GetAsyncPublisher<int, bool>().Publish(MessageKey.GameStage.ReturnTitle, true);
            });
        }
    }
}