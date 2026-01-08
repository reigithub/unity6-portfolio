using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Core.Extensions;
using Game.Core.Scenes;
using R3;
using TMPro;
using UnityEngine;

namespace Game.Contents.Scenes
{
    public class GameStageSceneComponent : GameSceneComponent
    {
        [SerializeField] private CanvasGroup _uiCanvasGroup;

        [SerializeField] private TextMeshProUGUI _limitTime;

        [SerializeField] private TextMeshProUGUI _currentPoint;
        [SerializeField] private TextMeshProUGUI _maxPoint;

        public void Initialize(GameStageSceneModel sceneModel)
        {
            _limitTime.text = sceneModel.CurrentTime.Value.FormatToTimer();
            _currentPoint.text = sceneModel.CurrentPoint.ToString();
            _maxPoint.text = sceneModel.MaxPoint.ToString();

            sceneModel.CurrentTime.DistinctUntilChanged().Subscribe(x => { _limitTime.text = x.FormatToTimer(); }).AddTo(this);
            sceneModel.CurrentPoint.DistinctUntilChanged().Subscribe(x => { _currentPoint.text = x.ToString(); }).AddTo(this);
        }

        private void Awake()
        {
            _uiCanvasGroup.alpha = 0f;
        }

        public void DoFadeIn()
        {
            _uiCanvasGroup.DOFade(1f, 0.25f);
        }

        public void DoFadeOut()
        {
            _uiCanvasGroup.DOFade(0f, 0.25f);
        }
    }
}