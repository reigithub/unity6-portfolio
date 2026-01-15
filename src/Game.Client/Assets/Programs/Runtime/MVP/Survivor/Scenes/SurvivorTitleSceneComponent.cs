using Game.MVP.Core.Scenes;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// Survivorタイトルシーンのルートコンポーネント
    /// UI要素の管理とイベント発行を担当
    /// </summary>
    public class SurvivorTitleSceneComponent : GameSceneComponent
    {
        [Header("Buttons")]
        [SerializeField] private Button _startGameButton;

        [SerializeField] private Button _returnButton;

        [SerializeField] private Button _quitButton;

        private readonly Subject<Unit> _onStartGameClicked = new();
        private readonly Subject<Unit> _onReturnClicked = new();
        private readonly Subject<Unit> _onQuitClicked = new();

        public Observable<Unit> OnStartGameClicked => _onStartGameClicked;
        public Observable<Unit> OnReturnClicked => _onReturnClicked;
        public Observable<Unit> OnQuitClicked => _onQuitClicked;

        protected override void OnDestroy()
        {
            _onStartGameClicked.Dispose();
            _onReturnClicked.Dispose();
            _onQuitClicked.Dispose();
            base.OnDestroy();
        }

        protected void Awake()
        {
            SetupButtons();
        }

        private void SetupButtons()
        {
            if (_startGameButton != null)
            {
                _startGameButton.OnClickAsObservable()
                    .Subscribe(_ => _onStartGameClicked.OnNext(Unit.Default))
                    .AddTo(Disposables);
            }

            if (_returnButton != null)
            {
                _returnButton.OnClickAsObservable()
                    .Subscribe(_ => _onReturnClicked.OnNext(Unit.Default))
                    .AddTo(Disposables);
            }
        }
    }
}