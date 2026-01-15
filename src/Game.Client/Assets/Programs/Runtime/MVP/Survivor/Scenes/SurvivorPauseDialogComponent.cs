using Game.MVP.Core.Scenes;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// Survivorポーズダイアログのルートコンポーネント
    /// </summary>
    public class SurvivorPauseDialogComponent : GameSceneComponent
    {
        [Header("Buttons")]
        [SerializeField] private Button _resumeButton;

        [SerializeField] private Button _retryButton;
        [SerializeField] private Button _quitButton;

        private readonly Subject<SurvivorPauseResult> _onResultSelected = new();
        public Observable<SurvivorPauseResult> OnResultSelected => _onResultSelected;

        protected override void OnDestroy()
        {
            _onResultSelected.Dispose();
            base.OnDestroy();
        }

        protected void Awake()
        {
            SetupButtons();
        }

        private void SetupButtons()
        {
            if (_resumeButton != null)
            {
                _resumeButton.OnClickAsObservable()
                    .Subscribe(_ => _onResultSelected.OnNext(SurvivorPauseResult.Resume))
                    .AddTo(Disposables);
            }

            if (_retryButton != null)
            {
                _retryButton.OnClickAsObservable()
                    .Subscribe(_ => _onResultSelected.OnNext(SurvivorPauseResult.Retry))
                    .AddTo(Disposables);
            }

            if (_quitButton != null)
            {
                _quitButton.OnClickAsObservable()
                    .Subscribe(_ => _onResultSelected.OnNext(SurvivorPauseResult.Quit))
                    .AddTo(Disposables);
            }
        }
    }
}