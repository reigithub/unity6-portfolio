using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Core.UI;
using Game.Horror.SaveData;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using Game.Shared.Enums;
using Game.Shared.Extensions;
using R3;
using UnityEngine;

namespace Game.Horror.Dialogs
{
    public class HorrorOptionDialog : GameDialogScene<HorrorOptionDialog, HorrorOptionDialogComponent, bool>
    {
        protected override string AssetPathOrAddress => "HorrorOptionDialog";

        private InputSystemService _inputService;

        private HorrorOptionSaveService _optionSaveService;
        private HorrorOptionSaveData Options => _optionSaveService.Data;

        // 進行中のリバインド操作（多重開始防止 / キャンセルボタン連動用）。null = 非実行中。
        private IDisposable _currentRebind;

        public static async UniTask<bool> RunAsync()
        {
            var sceneService = GameServiceManager.Get<GameSceneService>();
            return await sceneService.TransitionDialogAsync<HorrorOptionDialog, bool>();
        }

        public override UniTask PreInitialize()
        {
            _inputService = GameServiceManager.Get<InputSystemService>();
            _optionSaveService = GameServiceManager.Resolve<HorrorOptionSaveService>();
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

            // General
            SceneComponent.OnLanguageChanged
                .Subscribe(code =>
                {
                    _optionSaveService.SetLanguageCode(code);
                    HorrorOptionHelper.ApplyLanguage(code);
                })
                .AddTo(Disposables);
            SceneComponent.OnCameraControlHorizontalChanged
                .Subscribe(b => { _optionSaveService.SetCameraControlHorizontal(b); })
                .AddTo(Disposables);
            SceneComponent.OnCameraControlVerticalChanged
                .Subscribe(b => { _optionSaveService.SetCameraControlVertical(b); })
                .AddTo(Disposables);
            SceneComponent.OnCameraSensitivityHorizontalChanged
                .Subscribe(v => { _optionSaveService.SetCameraSensitivityHorizontal(v); })
                .AddTo(Disposables);
            SceneComponent.OnCameraSensitivityVerticalChanged
                .Subscribe(v => { _optionSaveService.SetCameraSensitivityVertical(v); })
                .AddTo(Disposables);
            SceneComponent.OnCameraAccelerationChanged
                .Subscribe(v => { _optionSaveService.SetCameraAcceleration(v); })
                .AddTo(Disposables);
            SceneComponent.OnCameraShakeChanged
                .Subscribe(v => { _optionSaveService.SetCameraShake(v); })
                .AddTo(Disposables);
            SceneComponent.OnCameraFovChanged
                .Subscribe(v => { _optionSaveService.SetCameraFov(v); })
                .AddTo(Disposables);

            // Display
            SceneComponent.OnDisplayModeChanged
                .Subscribe(mode =>
                {
                    _optionSaveService.SetDisplayMode(mode);
                    HorrorOptionHelper.ApplyResolution(Options.DisplayMode, Options.ResolutionWidth, Options.ResolutionHeight);
                })
                .AddTo(Disposables);
            SceneComponent.OnResolutionChanged
                .Subscribe(res =>
                {
                    _optionSaveService.SetResolution(res.Width, res.Height);
                    HorrorOptionHelper.ApplyResolution(Options.DisplayMode, Options.ResolutionWidth, Options.ResolutionHeight);
                })
                .AddTo(Disposables);
            SceneComponent.OnFrameRateChanged
                .Subscribe(fps =>
                {
                    _optionSaveService.SetFrameRateLimit(Mathf.RoundToInt(fps));
                    HorrorOptionHelper.ApplyFrameRate(Options.VSync, Options.UncappedFrameRate, Options.FrameRateLimit);
                })
                .AddTo(Disposables);
            SceneComponent.OnUncappedFrameRateChanged
                .Subscribe(uncapped =>
                {
                    _optionSaveService.SetUncappedFrameRate(uncapped);
                    HorrorOptionHelper.ApplyFrameRate(Options.VSync, Options.UncappedFrameRate, Options.FrameRateLimit);
                })
                .AddTo(Disposables);
            SceneComponent.OnVSyncChanged
                .Subscribe(vsync =>
                {
                    _optionSaveService.SetVSync(vsync);
                    HorrorOptionHelper.ApplyFrameRate(Options.VSync, Options.UncappedFrameRate, Options.FrameRateLimit);
                })
                .AddTo(Disposables);

            // Input（キーリバインド）
            foreach (var rebindView in SceneComponent.RebindViews)
            {
                var rebind = rebindView;
                rebind.SetDisplay(_inputService.GetBindingDisplayString(rebind.ActionName, rebind.Scheme, rebind.CompositePartName));

                // 進行中（_currentRebind != null）は新規開始を弾き、多重リバインドを防ぐ
                rebind.OnRebindRequested
                    .Where(_ => State.IsProcessing() && _currentRebind == null)
                    .Subscribe(_ =>
                    {
                        rebind.SetWaiting(true);
                        _currentRebind = _inputService.StartRebind(
                            rebind.ActionName,
                            rebind.Scheme,
                            rebind.CompositePartName,
                            display =>
                            {
                                rebind.SetWaiting(false);
                                rebind.SetDisplay(display);
                                _optionSaveService.SetInputBindingOverrides(_inputService.SaveBindingOverridesAsJson());
                                _currentRebind = null;
                            },
                            () =>
                            {
                                rebind.SetWaiting(false);
                                rebind.SetDisplay(_inputService.GetBindingDisplayString(rebind.ActionName, rebind.Scheme, rebind.CompositePartName));
                                _currentRebind = null;
                            });
                        _currentRebind.AddTo(Disposables);
                    })
                    .AddTo(Disposables);

                rebind.OnResetRequested
                    .Where(_ => State.IsProcessing() && _currentRebind == null)
                    .Subscribe(_ =>
                    {
                        _inputService.ResetBinding(rebind.ActionName, rebind.Scheme, rebind.CompositePartName);
                        rebind.SetDisplay(_inputService.GetBindingDisplayString(rebind.ActionName, rebind.Scheme, rebind.CompositePartName));
                        _optionSaveService.SetInputBindingOverrides(_inputService.SaveBindingOverridesAsJson());
                    })
                    .AddTo(Disposables);

                rebind.OnCancelRequested
                    .Subscribe(_ => _currentRebind?.Dispose())
                    .AddTo(Disposables);
            }

            return base.Startup();
        }

        public override async UniTask Terminate()
        {
            await _optionSaveService.SaveIfDirtyAsync();
            await base.Terminate();
        }
    }

    public class HorrorOptionDialogComponent : GameSceneComponent
    {
        #region SerializeField

        [SerializeField] private TabGroup _tabGroup;

        [Header("Options - General")]
        [SerializeField] private SliderIndexSelector _language;
        [SerializeField] private GenericValues<string> _languageValues;

        [SerializeField] private SliderBooleanSelector _cameraControlHorizontal;
        [SerializeField] private SliderBooleanSelector _cameraControlVertical;

        [SerializeField] private SliderValueSelector _cameraSensitivityHorizontal;
        [SerializeField] private SliderValueSelector _cameraSensitivityVertical;

        [SerializeField] private SliderValueSelector _cameraAcceleration;
        [SerializeField] private SliderValueSelector _cameraShake;
        [SerializeField] private SliderValueSelector _cameraFov;

        [Header("Options - Display")]
        [SerializeField] private SliderIndexSelector _displayMode;
        [SerializeField] private GenericValues<FullScreenMode> _displayModeValues;

        [SerializeField] private SliderIndexSelector _resolution;
        [SerializeField] private GenericValues<ResolutionInfo> _resolutionValues;

        [SerializeField] private SliderValueSelector _frameRate;
        [SerializeField] private SliderBooleanSelector _uncappedFrameRate;
        [SerializeField] private SliderBooleanSelector _vSync;
        [SerializeField] private SliderBooleanSelector _motionBlur;

        [Header("Options - Graphics")]
        [SerializeField] private SliderIndexSelector _graphicQualityPreset;
        [SerializeField] private GenericValues<GraphicQuality> _graphicQualityValues;

        [SerializeField] private SliderValueSelector _resolutionScale;

        [SerializeField] private SliderIndexSelector _lighting;
        [SerializeField] private SliderIndexSelector _reflection;
        [SerializeField] private SliderIndexSelector _antiAliasing;
        [SerializeField] private SliderIndexSelector _postProcessing;

        [Header("Options - Audio")]
        [SerializeField] private SliderValueSelector _masterVolume;
        [SerializeField] private SliderValueSelector _bgmVolume;
        [SerializeField] private SliderValueSelector _voiceVolume;
        [SerializeField] private SliderValueSelector _seVolume;

        [Header("Options - Input")]
        [SerializeField] private InputActionRebindView[] _rebindViews;

        #endregion

        #region Options - General

        public Observable<string> OnLanguageChanged => _language.OnValueChanged.Select(index => _languageValues[index]);

        public Observable<bool> OnCameraControlHorizontalChanged => _cameraControlHorizontal.OnValueChanged;
        public Observable<bool> OnCameraControlVerticalChanged => _cameraControlVertical.OnValueChanged;
        public Observable<float> OnCameraSensitivityHorizontalChanged => _cameraSensitivityHorizontal.OnValueChanged;
        public Observable<float> OnCameraSensitivityVerticalChanged => _cameraSensitivityVertical.OnValueChanged;
        public Observable<float> OnCameraAccelerationChanged => _cameraAcceleration.OnValueChanged;
        public Observable<float> OnCameraShakeChanged => _cameraShake.OnValueChanged;
        public Observable<float> OnCameraFovChanged => _cameraFov.OnValueChanged;

        #endregion

        #region Options - Display

        public Observable<FullScreenMode> OnDisplayModeChanged => _displayMode.OnValueChanged.Select(index => _displayModeValues[index]);
        public Observable<ResolutionInfo> OnResolutionChanged => _resolution.OnValueChanged.Select(index => _resolutionValues[index]);
        public Observable<float> OnFrameRateChanged => _frameRate.OnValueChanged;
        public Observable<bool> OnUncappedFrameRateChanged => _uncappedFrameRate.OnValueChanged;
        public Observable<bool> OnVSyncChanged => _vSync.OnValueChanged;
        public Observable<bool> OnMotionBlurChanged => _motionBlur.OnValueChanged;

        #endregion

        #region Options - Graphics

        public Observable<GraphicQuality> OnGraphicQualityPresetChanged => _graphicQualityPreset.OnValueChanged.Select(index => _graphicQualityValues[index]);

        public Observable<float> OnResolutionScaleChanged => _resolutionScale.OnValueChanged;

        public Observable<GraphicQuality> OnLightingChanged => _lighting.OnValueChanged.Select(index => _graphicQualityValues[index]);
        public Observable<GraphicQuality> OnReflectionChanged => _reflection.OnValueChanged.Select(index => _graphicQualityValues[index]);
        public Observable<GraphicQuality> OnAntiAliasingChanged => _antiAliasing.OnValueChanged.Select(index => _graphicQualityValues[index]);
        public Observable<GraphicQuality> OnPostProcessingChanged => _postProcessing.OnValueChanged.Select(index => _graphicQualityValues[index]);

        #endregion

        #region Audio

        public Observable<float> OnMasterVolumeChanged => _masterVolume.OnValueChanged;
        public Observable<float> OnBgmVolumeChanged => _bgmVolume.OnValueChanged;
        public Observable<float> OnVoiceVolumeChanged => _voiceVolume.OnValueChanged;
        public Observable<float> OnSeVolumeChanged => _seVolume.OnValueChanged;

        #endregion

        #region Input

        /// <summary>キーリバインド行（アクション×スキーム単位）。Dialog 側が購読・表示更新する。</summary>
        public IReadOnlyList<InputActionRebindView> RebindViews => _rebindViews;

        #endregion

        public void Initialize(HorrorOptionSaveData d)
        {
            _tabGroup.Initialize();

            // General
            _language.SetIndex(_languageValues[d.LanguageCode]);
            _cameraControlHorizontal.SetBool(d.CameraControlHorizontal);
            _cameraControlVertical.SetBool(d.CameraControlVertical);
            _cameraSensitivityHorizontal.SetValue(d.CameraSensitivityHorizontal);
            _cameraSensitivityVertical.SetValue(d.CameraSensitivityVertical);
            _cameraAcceleration.SetValue(d.CameraAcceleration);
            _cameraShake.SetValue(d.CameraShake);
            _cameraFov.SetValue(d.CameraFov);

            // Display
            _displayMode.SetIndex(_displayModeValues[d.DisplayMode]);
            _resolution.SetIndex(ResolveResolutionIndex(d.ResolutionWidth, d.ResolutionHeight));
            _frameRate.SetValue(d.FrameRateLimit);
            _uncappedFrameRate.SetBool(d.UncappedFrameRate);
            _vSync.SetBool(d.VSync);

            _tabGroup.ChangeTab(0);
        }

        public void NextTab() => _tabGroup.NextTab();
        public void PreviousTab() => _tabGroup.PreviousTab();

        private int ResolveResolutionIndex(int width, int height)
        {
            var w = width > 0 ? width : Screen.currentResolution.width;
            var h = height > 0 ? height : Screen.currentResolution.height;
            for (int i = 0; i < _resolutionValues.Count; i++)
            {
                var resolution = _resolutionValues[i];
                if (resolution.Width == w && resolution.Height == h)
                    return i;
            }
            return 0;
        }
    }
}
