using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Shared.Extensions;
using Game.Core.MessagePipe;
using Game.Core.Services;
using Game.ScoreTimeAttack.Data;
using Game.Library.Shared.Enums;
using Game.Client.MasterData;
using Game.MVC.Core.Scenes;
using Game.Shared.Services;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.ScoreTimeAttack.Scenes
{
    public class ScoreTimeAttackTotalResultSceneComponent : GameSceneComponent
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

        private IAudioService _audioService;
        private IGameSceneService _sceneService;
        private IMasterDataService _masterDataService;
        private IMessagePipeService _messagePipeService;

        private ScoreTimeAttackStageTotalResultMaster _totalResultMaster;

        public void Initialize(ScoreTimeAttackStageTotalResultData data)
        {
            _audioService = GameServiceManager.Resolve<IAudioService>();
            _sceneService = GameServiceManager.Resolve<IGameSceneService>();
            _masterDataService = GameServiceManager.Resolve<IMasterDataService>();
            _messagePipeService = GameServiceManager.Resolve<IMessagePipeService>();

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

            var score = data.CalculateTotalScore();
            _score.text = score.ToString();

            var totalResultMasters = _masterDataService.MemoryDatabase.ScoreTimeAttackStageTotalResultMasterTable.All;
            _totalResultMaster = totalResultMasters
                .Where(x => x.TotalScore <= score)
                .OrderByDescending(x => x.TotalScore)
                .FirstOrDefault() ?? totalResultMasters.Last;
            _result.text = _totalResultMaster?.TotalRank ?? "Failed...";

            _returnButton.OnClickAsObservableThrottleFirst()
                .SubscribeAwait(async (_, token) =>
                {
                    SetInteractable(false);
                    _audioService.StopBgmAsync(token).Forget();
                    await _audioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StageReturnTitle, token);
                    await _sceneService.TransitionAsync<ScoreTimeAttackTitleScene>();
                })
                .AddTo(this);
        }

        public override UniTask Ready()
        {
            if (_totalResultMaster != null)
            {
                var ids = new[] { _totalResultMaster.BgmAudioId, _totalResultMaster.VoiceAudioId, _totalResultMaster.SoundEffectAudioId };
                _audioService.PlayAsync(ids).Forget();
                _messagePipeService.Publish(MessageKey.Player.PlayAnimation, _totalResultMaster.AnimatorStateName);
            }

            return base.Ready();
        }
    }
}
