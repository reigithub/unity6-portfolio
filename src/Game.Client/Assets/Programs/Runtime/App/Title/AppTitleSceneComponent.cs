using System.Threading;
using Cysharp.Threading.Tasks;
using Game.App.Services;
using Game.Library.Shared.Enums;
using Game.Shared.Enums;
using Game.Shared.Extensions;
using Game.Shared.Services;
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

        private IAppServiceProvider _serviceProvider;
        private IAudioService AudioService => _serviceProvider?.AudioService;

        private void Awake()
        {
            _onGameModeSelected = new Subject<GameMode>();
        }

        public void Initialize(IAppServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;

            if (_scoreTimeAttackButton != null)
            {
                _scoreTimeAttackButton.OnClickAsObservable()
                    .SubscribeAwait(async (_, token) =>
                    {
                        SetButtonsInteractable(false);
                        await PlayGameStartSoundAsync(token);
                        _onGameModeSelected.OnNext(GameMode.MvcScoreTimeAttack);
                    }).AddTo(this);
            }

            if (_survivorButton != null)
            {
                _survivorButton.OnClickAsObservable()
                    .SubscribeAwait(async (_, token) =>
                    {
                        SetButtonsInteractable(false);
                        await PlayGameStartSoundAsync(token);
                        _onGameModeSelected.OnNext(GameMode.MvpSurvivor);
                    }).AddTo(this);
            }

            if (_quitButton != null)
            {
                _quitButton.OnClickAsObservable()
                    .SubscribeAwait(async (_, token) =>
                    {
                        SetButtonsInteractable(false);
                        await PlayGameStartSoundAsync(token);
#if UNITY_EDITOR
                        UnityEditor.EditorApplication.ExitPlaymode();
#else
                        Application.Quit();
#endif
                    }).AddTo(this);
            }

            PlayGameReadySoundAsync().ForgetWithHandler("AppTitleSceneComponent.PlayGameReadySound");
        }

        private async UniTask PlayGameReadySoundAsync()
        {
            _animator.Play(_animatorStateName);

            if (AudioService != null)
            {
                await AudioService.PlayRandomOneAsync(AudioPlayTag.GameReady);
            }
        }

        private async UniTask PlayGameStartSoundAsync(CancellationToken token)
        {
            if (AudioService != null)
            {
                AudioService.PlayRandomOneAsync(AudioCategory.SoundEffect, AudioPlayTag.UIButton, token).ForgetWithHandler("AppTitleSceneComponent.PlayUIButtonSound");
                await AudioService.PlayRandomOneAsync(AudioPlayTag.GameStart, token);
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