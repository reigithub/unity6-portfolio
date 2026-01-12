using Game.Shared.Enums;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.App.Title
{
    /// <summary>
    /// アプリタイトル画面のUIコンポーネント
    /// ゲームモード選択
    /// </summary>
    public class AppTitleSceneComponent : MonoBehaviour
    {
        [SerializeField] private Button _scoreTimeAttackButton;
        [SerializeField] private Button _survivorButton;
        [SerializeField] private Button _quitButton;

        [SerializeField] private Animator _animator;
        [SerializeField] private string _animatorStateName = "Salute";

        private Subject<GameMode> _onGameModeSelected;
        public Observable<GameMode> OnGameModeSelected => _onGameModeSelected;

        private void Awake()
        {
            _onGameModeSelected = new Subject<GameMode>();
        }

        public void Initialize()
        {
            _animator.Play(_animatorStateName);

            if (_scoreTimeAttackButton != null)
            {
                _scoreTimeAttackButton.OnClickAsObservable().Subscribe(_ =>
                {
                    SetButtonsInteractable(false);
                    _onGameModeSelected.OnNext(GameMode.MvcScoreTimeAttack);
                }).AddTo(this);
            }

            if (_survivorButton != null)
            {
                _survivorButton.OnClickAsObservable().Subscribe(_ =>
                {
                    SetButtonsInteractable(false);
                    // Coming Soon...
                    // _onGameModeSelected.OnNext(GameMode.MvpSurvivor);
                }).AddTo(this);
            }

            if (_quitButton != null)
            {
                _quitButton.OnClickAsObservable().Subscribe(_ =>
                {
                    SetButtonsInteractable(false);
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.ExitPlaymode();
#else
                    Application.Quit();
#endif
                });
            }
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (_scoreTimeAttackButton != null) _scoreTimeAttackButton.interactable = interactable;
            if (_survivorButton != null) _survivorButton.interactable = interactable;
            if (_quitButton != null) _quitButton.interactable = interactable;
        }

        private void OnDestroy()
        {
            _onGameModeSelected?.Dispose();
        }
    }
}