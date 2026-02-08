using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.App.Services;
using Game.Library.Shared.Enums;
using Game.Shared.Enums;
using Game.Shared.Extensions;
using Game.Shared.Services;
using Game.Shared.Services.RemoteAsset;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.App.Title
{
    /// <summary>
    /// アプリタイトル画面のUIコンポーネント（UI Toolkit版）
    /// GameStartボタン → リモートアセットダウンロード → ゲームモード選択
    /// </summary>
    public class AppTitleSceneComponent : MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;
        [SerializeField] private Animator _animator;
        [SerializeField] private string _animatorStateName = "Salute";

        private Subject<GameMode> _onGameModeSelected;
        public Observable<GameMode> OnGameModeSelected => _onGameModeSelected;

        private IAppServiceProvider _serviceProvider;
        private IAudioService AudioService => _serviceProvider?.AudioService;
        private IRemoteAssetDownloadService DownloadService => _serviceProvider?.RemoteAssetDownloadService;

        private CancellationTokenSource _cts;

        // UI Elements
        private VisualElement _root;
        private VisualElement _initialPanel;
        private VisualElement _downloadingPanel;
        private VisualElement _errorPanel;
        private VisualElement _modeSelectionPanel;

        private Button _gameStartButton;
        private Button _retryButton;
        private Button _scoreTimeAttackButton;
        private Button _survivorButton;
        private Button _quitButton;

        private Label _downloadStatus;
        private ProgressBar _downloadProgress;
        private Label _downloadSize;
        private Label _errorMessage;
        private Label _versionText;

        private void Awake()
        {
            _onGameModeSelected = new Subject<GameMode>();
            _cts = new CancellationTokenSource();
        }

        public void Initialize(IAppServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;

            SetupUIElements();
            BindButtons();
            ShowPanel(_initialPanel);
            UpdateVersionText();

            PlayGameReadySoundAsync().ForgetWithHandler("AppTitleSceneComponent.PlayGameReadySound");
        }

        private void SetupUIElements()
        {
            _root = _uiDocument.rootVisualElement;

            // Panels
            _initialPanel = _root.Q<VisualElement>("initial-panel");
            _downloadingPanel = _root.Q<VisualElement>("downloading-panel");
            _errorPanel = _root.Q<VisualElement>("error-panel");
            _modeSelectionPanel = _root.Q<VisualElement>("mode-selection-panel");

            // Buttons
            _gameStartButton = _root.Q<Button>("game-start-button");
            _retryButton = _root.Q<Button>("retry-button");
            _scoreTimeAttackButton = _root.Q<Button>("score-timeattack-button");
            _survivorButton = _root.Q<Button>("survivor-button");
            _quitButton = _root.Q<Button>("quit-button");

            // Labels & Progress
            _downloadStatus = _root.Q<Label>("download-status");
            _downloadProgress = _root.Q<ProgressBar>("download-progress");
            _downloadSize = _root.Q<Label>("download-size");
            _errorMessage = _root.Q<Label>("error-message");
            _versionText = _root.Q<Label>("version-text");
        }

        private void BindButtons()
        {
            _gameStartButton?.RegisterCallback<ClickEvent>(_ => OnGameStartClicked());
            _retryButton?.RegisterCallback<ClickEvent>(_ => OnRetryClicked());

            _scoreTimeAttackButton?.RegisterCallback<ClickEvent>(_ =>
            {
                SetModeButtonsEnabled(false);
                SelectGameModeAsync(GameMode.MvcScoreTimeAttack).Forget();
            });

            _survivorButton?.RegisterCallback<ClickEvent>(_ =>
            {
                SetModeButtonsEnabled(false);
                SelectGameModeAsync(GameMode.MvpSurvivor).Forget();
            });

            _quitButton?.RegisterCallback<ClickEvent>(_ =>
            {
                SetModeButtonsEnabled(false);
                QuitGameAsync().Forget();
            });
        }

        private void OnGameStartClicked()
        {
            _gameStartButton.SetEnabled(false);
            HandleGameStartAsync().ForgetWithHandler("AppTitleSceneComponent.HandleGameStart");
        }

        private async UniTask HandleGameStartAsync()
        {
            // Local環境またはダウンロード済みの場合は直接モード選択へ
            if (DownloadService == null || !DownloadService.IsDownloadRequired)
            {
                ShowPanel(_modeSelectionPanel);
                return;
            }

            // リモート環境：ダウンロード開始
            await StartDownloadAsync();
        }

        private async UniTask StartDownloadAsync()
        {
            ShowPanel(_downloadingPanel);

            var progress = new Progress<DownloadProgress>(OnDownloadProgress);

            try
            {
                var success = await DownloadService.DownloadAssetsAsync(progress, _cts.Token);

                if (success)
                {
                    ShowPanel(_modeSelectionPanel);
                }
                else
                {
                    ShowError("ダウンロードに失敗しました");
                }
            }
            catch (OperationCanceledException)
            {
                // キャンセル時は何もしない
            }
            catch (RemoteAssetDownloadException ex)
            {
                ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AppTitleScene] Download error: {ex}");
                ShowError("予期しないエラーが発生しました");
            }
        }

        private void OnDownloadProgress(DownloadProgress progress)
        {
            switch (progress.Status)
            {
                case DownloadStatus.Checking:
                    _downloadStatus.text = progress.CurrentOperation;
                    _downloadProgress.value = 0;
                    _downloadSize.text = "";
                    break;

                case DownloadStatus.Downloading:
                    _downloadStatus.text = "ダウンロード中...";
                    _downloadProgress.value = progress.Progress * 100f;
                    _downloadSize.text = $"{FormatBytes(progress.DownloadedBytes)} / {FormatBytes(progress.TotalBytes)}";
                    break;

                case DownloadStatus.Completed:
                    _downloadStatus.text = "完了";
                    _downloadProgress.value = 100f;
                    break;

                case DownloadStatus.Failed:
                    ShowError(progress.ErrorMessage);
                    break;
            }
        }

        private void OnRetryClicked()
        {
            _retryButton.SetEnabled(false);
            StartDownloadAsync().ForgetWithHandler("AppTitleSceneComponent.RetryDownload");
        }

        private void ShowError(string message)
        {
            _errorMessage.text = message;
            ShowPanel(_errorPanel);
            _retryButton.SetEnabled(true);
        }

        private async UniTask SelectGameModeAsync(GameMode mode)
        {
            await PlayGameStartSoundAsync(_cts.Token);
            _onGameModeSelected.OnNext(mode);
        }

        private async UniTask QuitGameAsync()
        {
            await PlayGameStartSoundAsync(_cts.Token);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }

        private void ShowPanel(VisualElement panel)
        {
            _initialPanel?.AddToClassList("hidden");
            _downloadingPanel?.AddToClassList("hidden");
            _errorPanel?.AddToClassList("hidden");
            _modeSelectionPanel?.AddToClassList("hidden");

            panel?.RemoveFromClassList("hidden");
        }

        private void SetModeButtonsEnabled(bool enabled)
        {
            _scoreTimeAttackButton?.SetEnabled(enabled);
            _survivorButton?.SetEnabled(enabled);
            _quitButton?.SetEnabled(enabled);
        }

        private void UpdateVersionText()
        {
            if (_versionText != null)
            {
                _versionText.text = $"v{Application.version}";
            }
        }

        private async UniTask PlayGameReadySoundAsync()
        {
            if (_animator != null)
            {
                _animator.Play(_animatorStateName);
            }

            if (AudioService != null)
            {
                await AudioService.PlayRandomOneAsync(AudioPlayTag.GameReady);
            }
        }

        private async UniTask PlayGameStartSoundAsync(CancellationToken token)
        {
            if (AudioService != null)
            {
                AudioService.PlayRandomOneAsync(AudioCategory.SoundEffect, AudioPlayTag.UIButton, token)
                    .ForgetWithHandler("AppTitleSceneComponent.PlayUIButtonSound");
                await AudioService.PlayRandomOneAsync(AudioPlayTag.GameStart, token);
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _onGameModeSelected?.Dispose();
        }
    }
}
