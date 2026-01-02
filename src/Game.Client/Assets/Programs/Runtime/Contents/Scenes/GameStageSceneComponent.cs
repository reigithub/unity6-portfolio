using System;
using System.Threading.Tasks;
using DG.Tweening;
using Game.Core.Extensions;
using Game.Core.Scenes;
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

        [SerializeField] private TextMeshProUGUI _currentHp;
        [SerializeField] private TextMeshProUGUI _maxHp;

        private GameStageSceneModel _sceneModel;

        public Task Initialize(GameStageSceneModel sceneModel)
        {
            _sceneModel = sceneModel;
            UpdateView();
            return Task.CompletedTask;
        }

        private void Awake()
        {
            _uiCanvasGroup.alpha = 0f;
        }

        public void UpdateLimitTime()
        {
            _limitTime.text = _sceneModel.CurrentTime.FormatToTimer();
        }

        public void UpdateView()
        {
            _currentPoint.text = _sceneModel.CurrentPoint.ToString();
            _maxPoint.text = _sceneModel.MaxPoint.ToString();

            _currentHp.text = _sceneModel.PlayerCurrentHp.ToString();
            _maxHp.text = _sceneModel.PlayerMaxHp.ToString();
        }

        public void DoFadeInView()
        {
            _uiCanvasGroup.DOFade(1f, 0.25f);
        }

        public void DoFadeOutView()
        {
            _uiCanvasGroup.DOFade(0f, 0.25f);
        }
    }
}