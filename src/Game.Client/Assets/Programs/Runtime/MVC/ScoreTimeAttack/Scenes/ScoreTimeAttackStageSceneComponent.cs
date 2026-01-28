using DG.Tweening;
using Game.Shared.Constants;
using Game.Shared.Extensions;
using Game.MVC.Core.Scenes;
using R3;
using TMPro;
using UnityEngine;

namespace Game.ScoreTimeAttack.Scenes
{
    public class ScoreTimeAttackStageSceneComponent : GameSceneComponent
    {
        [SerializeField] private CanvasGroup _uiCanvasGroup;

        [SerializeField] private TextMeshProUGUI _limitTime;

        [SerializeField] private TextMeshProUGUI _currentPoint;
        [SerializeField] private TextMeshProUGUI _maxPoint;

        public void Initialize(ScoreTimeAttackStageSceneModel sceneModel)
        {
            _limitTime.text = sceneModel.CurrentTime.Value.FormatToTimer();
            _currentPoint.text = sceneModel.CurrentPoint.ToString();
            _maxPoint.text = sceneModel.MaxPoint.ToString();

            sceneModel.CurrentTime.DistinctUntilChanged().Subscribe(x => { _limitTime.text = x.FormatToTimer(); }).AddTo(this);
            sceneModel.CurrentPoint.DistinctUntilChanged().Subscribe(x => { _currentPoint.text = x.ToString(); }).AddTo(this);
        }

        private void Awake()
        {
            _uiCanvasGroup.alpha = UIAnimationConstants.AlphaTransparent;
        }

        public void DoFadeIn()
        {
            _uiCanvasGroup.DOFade(UIAnimationConstants.AlphaOpaque, UIAnimationConstants.StandardFadeDuration);
        }

        public void DoFadeOut()
        {
            _uiCanvasGroup.DOFade(UIAnimationConstants.AlphaTransparent, UIAnimationConstants.StandardFadeDuration);
        }
    }
}