using System.Linq;
using Game.Core.Constants;
using Game.Core.Enums;
using Game.Core.Extensions;
using Game.Core.MasterData.MemoryTables;
using Game.Core.MessagePipe;
using Game.Core.Scenes;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Contents.Scenes
{
    public class GameTotalResultSceneComponent : GameSceneComponent
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
        private Button _returnButton;

        private StageTotalResultMaster _totalResultMaster;

        public void Initialize(GameStageTotalResultData data)
        {
            var currentTime = data.StageResults.Sum(x => x.CurrentTime);
            var totalTime = data.StageResults.Sum(x => x.TotalTime);
            _time.text = Mathf.Abs(currentTime - totalTime).FormatToTimer();

            var currentPoint = data.StageResults.Sum(x => x.CurrentPoint);
            var maxPoint = data.StageResults.Sum(x => x.MaxPoint);
            _point.text = currentPoint.ToString();
            _maxPoint.text = maxPoint.ToString();

            var currentHp = data.StageResults.Sum(x => x.PlayerCurrentHp);
            var maxHp = data.StageResults.Sum(x => x.PlayerMaxHp);
            _hp.text = currentHp.ToString();
            _maxHp.text = maxHp.ToString();

            var score = data.StageResults.Sum(x => x.CalculateScore());
            _score.text = score.ToString();

            _totalResultMaster = MemoryDatabase.StageTotalResultMasterTable.All
                .OrderByDescending(x => x.TotalScore)
                .FirstOrDefault(x => x.TotalScore > score);
            _result.text = _totalResultMaster?.TotalRank;

            _returnButton.OnClickAsObservableThrottleFirst()
                .SubscribeAwait(async (_, token) =>
                {
                    SetInteractiveAllButton(false);
                    AudioService.StopBgm();
                    await AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StageReturnTitle, token);
                    await SceneService.TransitionAsync<GameTitleScene>();
                })
                .AddTo(this);
        }

        public void Ready()
        {
            var ids = new[] { _totalResultMaster.BgmAudioId, _totalResultMaster.VoiceAudioId, _totalResultMaster.SoundEffectAudioId };
            AudioService.PlayAsync(ids).Forget();

            GlobalMessageBroker.GetPublisher<int, string>().Publish(MessageKey.Player.PlayAnimation, _totalResultMaster.AnimatorStateName);
        }
    }
}