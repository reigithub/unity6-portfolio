using System;
using System.Threading.Tasks;
using Game.Contents.Scenes;
using Game.Core.Extensions;
using Game.Core.MessagePipe;
using Game.Core.Scenes;
using Game.Core.Services;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Contents.UI
{
    public class GameResultUIDialog : GameDialogScene<GameResultUIDialog, GameResultUI, bool>
    {
        protected override string AssetPathOrAddress => "GameResultUI";

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
        private TextMeshProUGUI _time;

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
            if (data.StageResult == GameStageResult.Clear)
            {
                _result.color = Color.orange;
                _result.text = "Clear!";
            }
            else
            {
                _result.color = Color.red;
                _result.text = "Failed...";
            }

            _time.text = Mathf.Abs(data.CurrentTime - data.TotalTime).FormatToTimer();

            _point.text = data.CurrentPoint.ToString();
            _maxPoint.text = data.MaxPoint.ToString();

            _hp.text = data.PlayerCurrentHp.ToString();
            _maxHp.text = data.PlayerMaxHp.ToString();

            bool showNext = data.StageResult is GameStageResult.Clear && data.NextStageId.HasValue;
            _nextButton.gameObject.SetActive(showNext);
            if (showNext)
            {
                _nextButton.OnClickAsObservable()
                    .ThrottleFirst(TimeSpan.FromSeconds(3f))
                    .SubscribeAwait(async (_, token) =>
                    {
                        SetInteractable(false);
                        await GlobalMessageBroker.GetAsyncPublisher<int, int?>().PublishAsync(MessageKey.GameStage.Finish, data.NextStageId, token);
                        dialog.TrySetResult(true);
                    })
                    .AddTo(this);
            }

            _returnButton.OnClickAsObservable()
                .ThrottleFirst(TimeSpan.FromSeconds(3f))
                .SubscribeAwait(async (_, token) =>
                {
                    SetInteractable(false);
                    await GlobalMessageBroker.GetAsyncPublisher<int, bool>().PublishAsync(MessageKey.GameStage.ReturnTitle, true, token);
                    dialog.TrySetResult(false);
                })
                .AddTo(this);
        }

        private void SetInteractable(bool interactable)
        {
            _nextButton.interactable = interactable;
            _returnButton.interactable = interactable;
        }
    }
}