using Cysharp.Threading.Tasks;
using Game.ScoreTimeAttack.Enums;
using Game.Shared.Extensions;
using Game.Core.MessagePipe;
using Game.Core.Services;
using Game.Library.Shared.Enums;
using Game.MVC.Core.Scenes;
using Game.ScoreTimeAttack.Data;
using Game.Shared.Bootstrap;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.ScoreTimeAttack.UI
{
    public enum ResultDialogResult
    {
        NextStage,
        Finish,
        ReturnToTitle
    }

    public class GameResultUIDialog : GameDialogScene<GameResultUIDialog, GameResultUI, ResultDialogResult>
    {
        protected override string AssetPathOrAddress => "GameResultUI";

        private AudioService _audioService;
        private AudioService AudioService => _audioService ??= GameServiceManager.Get<AudioService>();

        public static UniTask<ResultDialogResult> RunAsync(ScoreTimeAttackStageResultData data)
        {
            var sceneService = GameServiceManager.Get<GameSceneService>();
            return sceneService.TransitionDialogAsync<GameResultUIDialog, GameResultUI, ResultDialogResult>(
                initializer: (component, result) =>
                {
                    component.Initialize(result, data);
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
            if (SceneComponent.Data.StageResult is GameStageResult.Clear)
                AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StageClear).Forget();
            else
                AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StageFailed).Forget();
            return base.Ready();
        }

        public override UniTask Terminate()
        {
            if (Result != ResultDialogResult.ReturnToTitle)
            {
                ApplicationEvents.ResumeTime();
            }

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

        public ScoreTimeAttackStageResultData Data { get; private set; }

        public void Initialize(IGameSceneResult<ResultDialogResult> result, ScoreTimeAttackStageResultData data)
        {
            Data = data;

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
                    .Subscribe(_ =>
                    {
                        SetInteractable(false);
                        result.TrySetResult(ResultDialogResult.NextStage);
                    })
                    .AddTo(this);
            }

            bool showReturn = data.NextStageId.HasValue;
            _returnButton.gameObject.SetActive(showReturn);
            if (showReturn)
            {
                _returnButton.OnClickAsObservableThrottleFirst()
                    .Subscribe(_ =>
                    {
                        SetInteractable(false);
                        result.TrySetResult(ResultDialogResult.ReturnToTitle);
                    })
                    .AddTo(this);
            }

            bool showTotalResult = !data.NextStageId.HasValue || data.StageResult == GameStageResult.Failed;
            _totalResultButton.gameObject.SetActive(showTotalResult);
            if (showTotalResult)
            {
                _totalResultButton.OnClickAsObservableThrottleFirst()
                    .Subscribe(_ =>
                    {
                        SetInteractable(false);
                        result.TrySetResult(ResultDialogResult.Finish);
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