using System;
using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.SaveData;
using Game.Horror.Services;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using Game.Shared.Constants;
using Game.Shared.Extensions;
using Game.Shared.Input;
using Game.Shared.Localization;
using R3;
using UnityEngine;

namespace Game.Horror.Dialogs
{
    public class HorrorOptionDialog : GameDialogScene<HorrorOptionDialog, HorrorOptionDialogComponent, bool>
    {
        protected override string AssetPathOrAddress => "HorrorOptionDialog";

        private InputSystemService _inputService;
        private AudioService _audioService;

        private HorrorOptionSaveRepository _optionSaveRepository;
        private HorrorOptionSaveData Options => _optionSaveRepository.Data;

        // 進行中のリバインド操作（多重開始防止 / キャンセルボタン連動用）。null = 非実行中。
        private IDisposable _currentRebind;

        // 進行中リバインドの自動キャンセルタイマー（残り時間バー駆動）。_currentRebind と対で管理。
        private IDisposable _rebindTimeout;

        public static async UniTask<bool> RunAsync()
        {
            var sceneService = GameServiceManager.Get<GameSceneService>();
            return await sceneService.TransitionDialogAsync<HorrorOptionDialog, bool>();
        }

        public override UniTask PreInitialize()
        {
            _inputService = GameServiceManager.Get<InputSystemService>();
            _audioService = GameServiceManager.Get<AudioService>();
            _optionSaveRepository = GameServiceManager.Resolve<HorrorOptionSaveRepository>();
            return base.PreInitialize();
        }

        public override UniTask Startup()
        {
            // ダイアログキャンセル
            Observable.Merge(_inputService.UI.Cancel.OnPerformedAsObservable(), _inputService.UI.Menu.OnPerformedAsObservable())
                .Where(_ => State.IsProcessing())
                .Subscribe(_ => TrySetResult(default))
                .AddTo(Disposables);

            // L1 (Previous) / R1 (Next) でタブ循環
            _inputService.UI.Previous.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing())
                .Subscribe(_ => SceneComponent.PreviousTab())
                .AddTo(Disposables);

            _inputService.UI.Next.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing())
                .Subscribe(_ => SceneComponent.NextTab())
                .AddTo(Disposables);

            SceneComponent.Initialize(Options);

            // Gameplay
            SceneComponent.OnLanguageChanged
                .Subscribe(code =>
                {
                    _optionSaveRepository.SetLanguageCode(code);
                    HorrorOptionHelper.ApplyLanguage(code);
                })
                .AddTo(Disposables);
            SceneComponent.OnCameraControlHorizontalChanged
                .Subscribe(b => { _optionSaveRepository.SetCameraControlHorizontal(b); })
                .AddTo(Disposables);
            SceneComponent.OnCameraControlVerticalChanged
                .Subscribe(b => { _optionSaveRepository.SetCameraControlVertical(b); })
                .AddTo(Disposables);
            SceneComponent.OnCameraSensitivityHorizontalChanged
                .Subscribe(v => { _optionSaveRepository.SetCameraSensitivityHorizontal(v); })
                .AddTo(Disposables);
            SceneComponent.OnCameraSensitivityVerticalChanged
                .Subscribe(v => { _optionSaveRepository.SetCameraSensitivityVertical(v); })
                .AddTo(Disposables);
            SceneComponent.OnCameraAccelerationChanged
                .Subscribe(v => { _optionSaveRepository.SetCameraAcceleration(v); })
                .AddTo(Disposables);
            SceneComponent.OnCameraShakeChanged
                .Subscribe(v => { _optionSaveRepository.SetCameraShake(v); })
                .AddTo(Disposables);
            SceneComponent.OnCameraFovChanged
                .Subscribe(v => { _optionSaveRepository.SetCameraFov(v); })
                .AddTo(Disposables);

            SceneComponent.OnSprintModeChanged
                .Subscribe(b => { _optionSaveRepository.SetSprintToggle(b); })
                .AddTo(Disposables);
            SceneComponent.OnCrouchModeChanged
                .Subscribe(b => { _optionSaveRepository.SetCrouchToggle(b); })
                .AddTo(Disposables);

            // Graphics
            SceneComponent.OnDisplayModeChanged
                .Subscribe(mode =>
                {
                    _optionSaveRepository.SetDisplayMode(mode);
                    HorrorOptionHelper.ApplyResolution(Options.DisplayMode, Options.ResolutionWidth, Options.ResolutionHeight);
                })
                .AddTo(Disposables);
            SceneComponent.OnResolutionChanged
                .Subscribe(res =>
                {
                    _optionSaveRepository.SetResolution(res.Width, res.Height);
                    HorrorOptionHelper.ApplyResolution(Options.DisplayMode, Options.ResolutionWidth, Options.ResolutionHeight);
                })
                .AddTo(Disposables);
            SceneComponent.OnFrameRateChanged
                .Subscribe(fps =>
                {
                    _optionSaveRepository.SetFrameRateLimit(Mathf.RoundToInt(fps));
                    HorrorOptionHelper.ApplyFrameRate(Options.VSync, Options.UncappedFrameRate, Options.FrameRateLimit);
                })
                .AddTo(Disposables);
            SceneComponent.OnUncappedFrameRateChanged
                .Subscribe(uncapped =>
                {
                    _optionSaveRepository.SetUncappedFrameRate(uncapped);
                    HorrorOptionHelper.ApplyFrameRate(Options.VSync, Options.UncappedFrameRate, Options.FrameRateLimit);
                })
                .AddTo(Disposables);
            SceneComponent.OnVSyncChanged
                .Subscribe(vsync =>
                {
                    _optionSaveRepository.SetVSync(vsync);
                    HorrorOptionHelper.ApplyFrameRate(Options.VSync, Options.UncappedFrameRate, Options.FrameRateLimit);
                })
                .AddTo(Disposables);

            // Audio
            SceneComponent.OnMasterVolumeChanged
                .Subscribe(volume =>
                {
                    _optionSaveRepository.SetMasterVolume(volume);
                    _audioService.SetVolume(Options.MasterVolume, Options.BgmVolume, Options.VoiceVolume, Options.SeVolume);
                })
                .AddTo(Disposables);
            SceneComponent.OnBgmVolumeChanged
                .Subscribe(volume =>
                {
                    _optionSaveRepository.SetBgmVolume(volume);
                    _audioService.SetVolume(Options.MasterVolume, Options.BgmVolume, Options.VoiceVolume, Options.SeVolume);
                })
                .AddTo(Disposables);
            SceneComponent.OnVoiceVolumeChanged
                .Subscribe(volume =>
                {
                    _optionSaveRepository.SetVoiceVolume(volume);
                    _audioService.SetVolume(Options.MasterVolume, Options.BgmVolume, Options.VoiceVolume, Options.SeVolume);
                })
                .AddTo(Disposables);
            SceneComponent.OnSeVolumeChanged
                .Subscribe(volume =>
                {
                    _optionSaveRepository.SetSeVolume(volume);
                    _audioService.SetVolume(Options.MasterVolume, Options.BgmVolume, Options.VoiceVolume, Options.SeVolume);
                })
                .AddTo(Disposables);

            // Controls（キーリバインド）
            foreach (var rebindView in SceneComponent.RebindViews)
            {
                var rebind = rebindView;
                rebind.SetDisplay(_inputService.GetBindingDisplayString(rebind.Scheme, rebind.ActionName, rebind.CompositePartName));

                // 進行中（_currentRebind != null）は新規開始を弾き、多重リバインドを防ぐ
                rebind.OnRebindRequested
                    .Where(_ => State.IsProcessing() && _currentRebind == null)
                    .Subscribe(_ =>
                    {
                        rebind.SetWaiting(true);
                        rebind.SetTimeoutProgress(1f);
                        _currentRebind = _inputService.StartRebind(
                            rebind.Scheme,
                            rebind.ActionName,
                            rebind.CompositePartName,
                            display =>
                            {
                                rebind.SetWaiting(false);
                                // rebind.SetDisplay(display);
                                _optionSaveRepository.SetInputBindingOverrides(_inputService.SaveBindingOverridesAsJson());
                                _currentRebind = null;
                                _rebindTimeout?.Dispose();
                                _rebindTimeout = null;
                                // swap で旧キーが移った相手行も含め全行を再表示（ターゲット行も更新される）
                                RefreshBindingDisplays();
                                _inputService.SetSelectedGameObject(rebind.Selectable.gameObject);
                            },
                            () =>
                            {
                                rebind.SetWaiting(false);
                                rebind.SetDisplay(_inputService.GetBindingDisplayString(rebind.Scheme, rebind.ActionName, rebind.CompositePartName));
                                _currentRebind = null;
                                _rebindTimeout?.Dispose();
                                _rebindTimeout = null;
                                _inputService.SetSelectedGameObject(rebind.Selectable.gameObject);
                            });
                        _currentRebind.AddTo(Disposables);

                        // 開始から3秒で自動キャンセル（完了していない時のみ）。残り時間をバーで提示。
                        var elapsed = 0f;
                        _rebindTimeout = Observable.EveryUpdate(UnityFrameProvider.Update)
                            .Subscribe(_ =>
                            {
                                elapsed += Time.unscaledDeltaTime; // ポーズ中(timeScale=0)でも進行
                                rebind.SetTimeoutProgress(1f - elapsed / InputConstants.RebindTimeoutSeconds);
                                if (elapsed >= InputConstants.RebindTimeoutSeconds)
                                    _currentRebind?.Dispose(); // → onCanceled 経路で表示復元＆タイマー停止
                            });
                        _rebindTimeout.AddTo(Disposables);
                    })
                    .AddTo(Disposables);
            }

            // 指定スキームのバインドのみ既定へ戻して全行を再表示・保存する。
            SceneComponent.OnResetSchemeBindingsRequested
                .Where(_ => State.IsProcessing() && _currentRebind == null)
                .Subscribe(scheme =>
                {
                    _inputService.ResetSchemeBindings(scheme);
                    RefreshBindingDisplays();
                    _optionSaveRepository.SetInputBindingOverrides(_inputService.SaveBindingOverridesAsJson());
                })
                .AddTo(Disposables);

            // ロケール変更でバインド表示名を再ローカライズ
            LocalizationEvents.OnLocaleChanged.Subscribe(_ => RefreshBindingDisplays()).AddTo(Disposables);

            // コントローラー接続/切替に追従して family 別表示を更新する
            InputSystemEvents.OnDeviceChanged.Subscribe(_ => RefreshBindingDisplays()).AddTo(Disposables);

            return base.Startup();
        }

        private void RefreshBindingDisplays()
        {
            if (_currentRebind != null) return;
            foreach (var rebind in SceneComponent.RebindViews)
                rebind.SetDisplay(_inputService.GetBindingDisplayString(rebind.Scheme, rebind.ActionName, rebind.CompositePartName));
        }

        public override async UniTask Terminate()
        {
            await _optionSaveRepository.SaveIfDirtyAsync();
            await base.Terminate();
        }
    }
}
