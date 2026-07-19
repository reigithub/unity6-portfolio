using System;
using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Core.UI;
using Game.Horror.Enums;
using Game.Horror.SaveData;
using Game.Horror.Services.Interfaces;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using Game.Shared.Constants;
using Game.Shared.Extensions;
using Game.Shared.Services;
using Game.Shared.Services.Interfaces;
using R3;
using UnityEngine;

namespace Game.Horror.Dialogs
{
    public class HorrorOptionDialog : GameDialogScene<HorrorOptionDialog, HorrorOptionDialogComponent, bool>
    {
        protected override string AssetPathOrAddress => "HorrorOptionDialog";

        private readonly IInputSystemService _inputService = GameServiceManager.Resolve<IInputSystemService>();
        private readonly IInputActionIconService _inputActionIconService = GameServiceManager.Resolve<IInputActionIconService>();
        private readonly IAudioService _audioService = GameServiceManager.Resolve<IAudioService>();
        private readonly ILocalizationService _localizationService = GameServiceManager.Resolve<ILocalizationService>();
        private readonly IHorrorOptionSaveRepository _optionSaveRepository = GameServiceManager.Resolve<IHorrorOptionSaveRepository>();
        private readonly IHorrorOptionService _optionService =  GameServiceManager.Resolve<IHorrorOptionService>();
        private HorrorOptionSaveData Options => _optionSaveRepository.Data;

        private HorrorOptionTabCategory _tabCategory;
        private HorrorOptionTabSubCategory _tabSubCategory;

        private IDisposable _currentRebinding;        // 進行中のリバインド操作（多重開始防止 / キャンセルボタン連動用）
        private IDisposable _currentRebindingTimeout; // 進行中リバインドの自動キャンセルタイマー（残り時間バー駆動）

        public static async UniTask<bool> RunAsync()
        {
            var sceneService = GameServiceManager.Resolve<IGameSceneService>();
            return await sceneService.TransitionDialogAsync<HorrorOptionDialog, bool>();
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

            _inputService.UI.Reset.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing())
                .Subscribe(_ => ResetSubCategory())
                .AddTo(Disposables);

            SceneComponent.Initialize(Options);

            SceneComponent.OnCategoryChanged
                .Subscribe(x =>
                {
                    _tabCategory = x;
                    SetInputActionGuide();
                })
                .AddTo(Disposables);
            SceneComponent.OnSubCategoryChanged
                .Subscribe(x =>
                {
                    _tabSubCategory = x;
                    SetInputActionGuide();
                })
                .AddTo(Disposables);
            SetInputActionGuide();

            // Gameplay
            SceneComponent.OnLanguageChanged
                .Subscribe(code =>
                {
                    _optionService.SetLanguageCode(code);
                    HorrorOptionHelper.ApplyLanguage(code);
                })
                .AddTo(Disposables);
            SceneComponent.OnCameraControlHorizontalChanged
                .Subscribe(b => { _optionService.SetCameraControlHorizontal(b); })
                .AddTo(Disposables);
            SceneComponent.OnCameraControlVerticalChanged
                .Subscribe(b => { _optionService.SetCameraControlVertical(b); })
                .AddTo(Disposables);
            SceneComponent.OnCameraSensitivityHorizontalChanged
                .Subscribe(v => { _optionService.SetCameraSensitivityHorizontal(v); })
                .AddTo(Disposables);
            SceneComponent.OnCameraSensitivityVerticalChanged
                .Subscribe(v => { _optionService.SetCameraSensitivityVertical(v); })
                .AddTo(Disposables);
            SceneComponent.OnCameraAccelerationChanged
                .Subscribe(v => { _optionService.SetCameraAcceleration(v); })
                .AddTo(Disposables);
            SceneComponent.OnCameraShakeChanged
                .Subscribe(v => { _optionService.SetCameraShake(v); })
                .AddTo(Disposables);
            SceneComponent.OnCameraFovChanged
                .Subscribe(v => { _optionService.SetCameraFov(v); })
                .AddTo(Disposables);

            // Controls - BasicSettings
            SceneComponent.OnSprintModeChanged
                .Subscribe(b => { _optionService.SetSprintToggle(b); })
                .AddTo(Disposables);
            SceneComponent.OnCrouchModeChanged
                .Subscribe(b => { _optionService.SetCrouchToggle(b); })
                .AddTo(Disposables);

            // Graphics
            SceneComponent.OnDisplayModeChanged
                .Subscribe(mode =>
                {
                    _optionService.SetDisplayMode(mode);
                    HorrorOptionHelper.ApplyResolution(Options.DisplayMode, Options.ResolutionWidth, Options.ResolutionHeight);
                })
                .AddTo(Disposables);
            SceneComponent.OnResolutionChanged
                .Subscribe(res =>
                {
                    _optionService.SetResolution(res.Width, res.Height);
                    HorrorOptionHelper.ApplyResolution(Options.DisplayMode, Options.ResolutionWidth, Options.ResolutionHeight);
                })
                .AddTo(Disposables);
            SceneComponent.OnFrameRateChanged
                .Subscribe(fps =>
                {
                    _optionService.SetFrameRateLimit(Mathf.RoundToInt(fps));
                    HorrorOptionHelper.ApplyFrameRate(Options.VSync, Options.UncappedFrameRate, Options.FrameRateLimit);
                })
                .AddTo(Disposables);
            SceneComponent.OnUncappedFrameRateChanged
                .Subscribe(uncapped =>
                {
                    _optionService.SetUncappedFrameRate(uncapped);
                    HorrorOptionHelper.ApplyFrameRate(Options.VSync, Options.UncappedFrameRate, Options.FrameRateLimit);
                })
                .AddTo(Disposables);
            SceneComponent.OnShowFrameRateChanged
                .Subscribe(show =>
                {
                    _optionService.SetShowFrameRate(show);
                })
                .AddTo(Disposables);
            SceneComponent.OnVSyncChanged
                .Subscribe(vsync =>
                {
                    _optionService.SetVSync(vsync);
                    HorrorOptionHelper.ApplyFrameRate(Options.VSync, Options.UncappedFrameRate, Options.FrameRateLimit);
                })
                .AddTo(Disposables);

            // Audio
            SceneComponent.OnMasterVolumeChanged
                .Subscribe(volume =>
                {
                    _optionService.SetMasterVolume(volume);
                    _audioService.SetVolume(Options.MasterVolume, Options.BgmVolume, Options.VoiceVolume, Options.SeVolume);
                })
                .AddTo(Disposables);
            SceneComponent.OnBgmVolumeChanged
                .Subscribe(volume =>
                {
                    _optionService.SetBgmVolume(volume);
                    _audioService.SetVolume(Options.MasterVolume, Options.BgmVolume, Options.VoiceVolume, Options.SeVolume);
                })
                .AddTo(Disposables);
            SceneComponent.OnVoiceVolumeChanged
                .Subscribe(volume =>
                {
                    _optionService.SetVoiceVolume(volume);
                    _audioService.SetVolume(Options.MasterVolume, Options.BgmVolume, Options.VoiceVolume, Options.SeVolume);
                })
                .AddTo(Disposables);
            SceneComponent.OnSeVolumeChanged
                .Subscribe(volume =>
                {
                    _optionService.SetSeVolume(volume);
                    _audioService.SetVolume(Options.MasterVolume, Options.BgmVolume, Options.VoiceVolume, Options.SeVolume);
                })
                .AddTo(Disposables);

            // Controls（キーリバインド）
            foreach (var rebindingView in SceneComponent.RebindingViews)
            {
                var rebinding = rebindingView;
                RefreshBindingDisplay(rebinding);

                // 進行中（_currentRebind != null）は新規開始を弾き、多重リバインドを防ぐ
                rebinding.OnRebindRequested
                    .Where(_ => State.IsProcessing() && _currentRebinding == null)
                    .Subscribe(_ =>
                    {
                        rebinding.SetWaiting(true);
                        rebinding.SetTimeoutProgress(1f);
                        _currentRebinding = _inputService.StartRebinding(
                            rebinding.ControlScheme,
                            rebinding.ActionName,
                            rebinding.CompositePartName,
                            () =>
                            {
                                rebinding.SetWaiting(false);
                                _optionService.SetInputBindingOverrides(_inputService.SaveBindingOverridesAsJson());
                                _currentRebinding = null;
                                _currentRebindingTimeout?.Dispose();
                                _currentRebindingTimeout = null;
                                // swap で旧キーが移った相手行も含め全行を再表示（ターゲット行も更新される）
                                RefreshBindingDisplays();
                                _inputService.SetSelectedGameObject(rebinding.Selectable.gameObject);
                            },
                            () =>
                            {
                                rebinding.SetWaiting(false);
                                _currentRebinding = null;
                                _currentRebindingTimeout?.Dispose();
                                _currentRebindingTimeout = null;
                                RefreshBindingDisplay(rebinding);
                                _inputService.SetSelectedGameObject(rebinding.Selectable.gameObject);
                            });
                        _currentRebinding.AddTo(Disposables);

                        // 開始から3秒で自動キャンセル（完了していない時のみ）。残り時間をバーで提示。
                        var elapsed = 0f;
                        _currentRebindingTimeout = Observable.EveryUpdate(UnityFrameProvider.Update)
                            .Subscribe(_ =>
                            {
                                const float TimeoutSec = InputConstants.RebindingTimeoutSeconds;
                                elapsed += Time.unscaledDeltaTime; // ポーズ中(timeScale=0)でも進行
                                rebinding.SetTimeoutProgress(1f - elapsed / TimeoutSec);
                                if (elapsed >= TimeoutSec)
                                    _currentRebinding?.Dispose(); // → onCanceled 経路で表示復元＆タイマー停止
                            });
                        _currentRebindingTimeout.AddTo(Disposables);
                    })
                    .AddTo(Disposables);
            }

            // 指定スキームのバインドのみ既定へ戻して全行を再表示・保存する。
            // SceneComponent.OnResetSchemeBindingsRequested
            //     .Where(_ => State.IsProcessing() && _currentRebinding == null)
            //     .Subscribe(scheme =>
            //     {
            //         ResetSchemeBindings(scheme);
            //     })
            //     .AddTo(Disposables);

            // ロケール変更でバインド表示名を再ローカライズ
            _localizationService.OnLocaleChanged.Subscribe(_ => RefreshBindingDisplays()).AddTo(Disposables);

            // コントローラー接続/切替に追従して family 別表示を更新する
            _inputService.OnDeviceChanged.Subscribe(_ => RefreshBindingDisplays()).AddTo(Disposables);

            return base.Startup();
        }

        private void ResetSubCategory()
        {
            if (_tabCategory is not HorrorOptionTabCategory.Controls) return;

            switch (_tabSubCategory)
            {
                case HorrorOptionTabSubCategory.KeyboardAndMouse:
                    ResetControlSchemeBindings(InputControlSchemes.KeyboardAndMouse);
                    break;
                case HorrorOptionTabSubCategory.Gamepad:
                    ResetControlSchemeBindings(InputControlSchemes.Gamepad);
                    break;
            }
        }

        private void ResetControlSchemeBindings(string scheme)
        {
            if (_currentRebinding != null) return;
            _inputService.ResetControlSchemeBindings(scheme);
            RefreshBindingDisplays();
            _optionService.SetInputBindingOverrides(_inputService.SaveBindingOverridesAsJson());
        }

        private void RefreshBindingDisplay(InputActionRebindingView view)
        {
            var info = _inputService.GetBindingInfo(view.ControlScheme, view.ActionMapName, view.ActionName, view.CompositePartName);
            view.SetDisplay(info.DisplayName);
            view.SetIcon(_inputActionIconService.GetSprite(info));
        }

        private void RefreshBindingDisplays()
        {
            if (_currentRebinding != null) return;
            foreach (var rebindingView in SceneComponent.RebindingViews)
                RefreshBindingDisplay(rebindingView);
        }

        private void SetInputActionGuide()
        {
            if (_tabCategory is HorrorOptionTabCategory.Controls
                && _tabSubCategory is HorrorOptionTabSubCategory.KeyboardAndMouse or HorrorOptionTabSubCategory.Gamepad)
            {
                SceneComponent.SetInputActionGuide(_inputService.UI.Submit, _inputService.UI.Cancel, _inputService.UI.Reset);
                return;
            }

            SceneComponent.SetInputActionGuide(_inputService.UI.Submit, _inputService.UI.Cancel);
        }

        public override async UniTask Terminate()
        {
            await _optionSaveRepository.SaveIfDirtyAsync();
            await base.Terminate();
        }
    }
}
