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
            return sceneService.TransitionDialogAsync<GameResultUIDialog, GameResultUI, bool>(
                initializer: (component, result) =>
                {
                    component.Initialize(result, data);
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
        private TextMeshProUGUI _score;

        [SerializeField]
        private Button _nextButton;

        [SerializeField]
        private Button _returnButton;

        [SerializeField]
        private Button _totalResultButton;

        public void Initialize(IGameSceneResult<bool> result, GameStageResultData data)
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

            _score.text = data.CalculateScore().ToString();

            bool showNext = data.StageResult is GameStageResult.Clear && data.NextStageId.HasValue;
            _nextButton.gameObject.SetActive(showNext);
            if (showNext)
            {
                _nextButton.OnClickAsObservableThrottleFirst()
                    .SubscribeAwait(async (_, token) =>
                    {
                        SetInteractable(false);
                        await GlobalMessageBroker.GetAsyncPublisher<int, bool>().PublishAsync(MessageKey.GameStage.Finish, true, token);
                        result.TrySetResult(true);
                    })
                    .AddTo(this);
            }

            bool showReturn = data.NextStageId.HasValue;
            _returnButton.gameObject.SetActive(showReturn);
            if (showReturn)
            {
                _returnButton.OnClickAsObservableThrottleFirst()
                    .SubscribeAwait(async (_, token) =>
                    {
                        SetInteractable(false);
                        await GlobalMessageBroker.GetAsyncPublisher<int, bool>().PublishAsync(MessageKey.GameStage.ReturnTitle, true, token);
                        result.TrySetResult(false);
                    })
                    .AddTo(this);
            }

            bool showTotalResult = !data.NextStageId.HasValue;
            _totalResultButton.gameObject.SetActive(showTotalResult);
            if (showTotalResult)
            {
                _totalResultButton.OnClickAsObservableThrottleFirst()
                    .SubscribeAwait(async (_, token) =>
                    {
                        SetInteractable(false);
                        await GlobalMessageBroker.GetAsyncPublisher<int, bool>().PublishAsync(MessageKey.GameStage.Finish, true, token);
                        result.TrySetResult(true);
                    })
                    .AddTo(this);
            }
        }

        private void SetInteractable(bool interactable)
        {
            _nextButton.interactable = interactable;
            _returnButton.interactable = interactable;
        }
    }
}