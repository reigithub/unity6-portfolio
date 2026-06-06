using System;
using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Core.UI;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using Game.Shared.Enums;
using Game.Shared.Extensions;
using R3;
using R3.Triggers;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace Game.Horror.Dialogs
{
    public class HorrorOptionDialog : GameDialogScene<HorrorOptionDialog, HorrorOptionDialogComponent, bool>
    {
        protected override string AssetPathOrAddress => "HorrorOptionDialog";

        private HorrorOptionDialogModel _model;

        private InputSystemService _inputService;
        private InputSystemService InputService => _inputService ??= GameServiceManager.Get<InputSystemService>();

        public static async UniTask<bool> RunAsync()
        {
            var sceneService = GameServiceManager.Get<GameSceneService>();
            return await sceneService.TransitionDialogAsync<HorrorOptionDialog, bool>();
        }

        public override UniTask PreInitialize()
        {
            _model = new HorrorOptionDialogModel();
            return base.PreInitialize();
        }

        public override UniTask Startup()
        {
            // ダイアログキャンセル
            Observable.Merge(InputService.UI.Cancel.OnPerformedAsObservable(), InputService.UI.Menu.OnPerformedAsObservable())
                .Where(_ => State.IsProcessing())
                .Subscribe(_ => TrySetResult(default))
                .AddTo(Disposables);

            // L1 (Previous) / R1 (Next) でタブ循環
            InputService.UI.Previous.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing())
                .Subscribe(_ => SceneComponent.PreviousTab())
                .AddTo(Disposables);

            InputService.UI.Next.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing())
                .Subscribe(_ => SceneComponent.NextTab())
                .AddTo(Disposables);

            SceneComponent.OnLanguageChanged
                .Subscribe(code =>
                {
                    _model.SetLocalCode(code);
                    var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
                    LocalizationSettings.SelectedLocale = locale;
                })
                .AddTo(Disposables);

            // Audio
            SceneComponent.OnMasterVolumeChanged.Subscribe(_model.SetMasterVolume).AddTo(Disposables);
            SceneComponent.OnBgmVolumeChanged.Subscribe(_model.SetBgmVolume).AddTo(Disposables);
            SceneComponent.OnVoiceVolumeChanged.Subscribe(_model.SetVoiceVolume).AddTo(Disposables);
            SceneComponent.OnSeVolumeChanged.Subscribe(_model.SetSeVolume).AddTo(Disposables);

            return base.Startup();
        }
    }

    public class HorrorOptionDialogComponent : GameSceneComponent
    {
        #region SerializeField

        [SerializeField] private TabGroup _tabGroup;

        [Header("Options - General")]
        [SerializeField] private SliderIndexSelector _language;
        [SerializeField] private GenericValues<string> _languageValues;

        [SerializeField] private SliderIndexSelector _cameraControlHorizontal;
        [SerializeField] private SliderIndexSelector _cameraControlVertical;

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

        [SerializeField] private SliderIndexSelector _frameRateLimit;
        [SerializeField] private GenericValues<FrameRateLimit> _frameRateLimitValues;

        [SerializeField] private SliderIndexSelector _vSync;
        [SerializeField] private SliderIndexSelector _motionBluer;

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

        #endregion

        #region Options - General

        public Observable<string> OnLanguageChanged => _language.OnValueChanged.Select(index => _languageValues[index]);

        public Observable<bool> OnCameraControlHorizontalChanged => _cameraControlHorizontal.OnValueChanged.Select(index => index != 0);
        public Observable<bool> OnCameraControlVerticalChanged => _cameraControlVertical.OnValueChanged.Select(index => index != 0);
        public Observable<float> OnCameraSensitivityHorizontalChanged => _cameraSensitivityHorizontal.OnValueChanged;
        public Observable<float> OnCameraSensitivityVerticalChanged => _cameraSensitivityVertical.OnValueChanged;
        public Observable<float> OnCameraAccelerationChanged => _cameraAcceleration.OnValueChanged;
        public Observable<float> OnCameraShakeChanged => _cameraShake.OnValueChanged;

        #endregion

        #region Options - Video

        public Observable<FullScreenMode> OnDisplayModeChanged => _displayMode.OnValueChanged.Select(index => _displayModeValues[index]);
        public Observable<ResolutionInfo> OnResolutionChanged => _resolution.OnValueChanged.Select(index => _resolutionValues[index]);
        public Observable<float> OnFovChanged => _cameraFov.OnValueChanged;

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

        public override async UniTask Startup()
        {
            _tabGroup.Initialize();
            Initialize();
            _tabGroup.ChangeTab(0);
            await base.Startup();
        }

        public void NextTab() => _tabGroup.NextTab();
        public void PreviousTab() => _tabGroup.PreviousTab();

        private void Initialize()
        {
            #region Language

#if UNITY_EDITOR
            foreach (var locale in LocalizationSettings.AvailableLocales.Locales)
            {
                Debug.Log($"Localization Code: {locale.Identifier.Code} / LocalName: {locale.LocaleName}");
            }
#endif

            // _language.OnValueChanged
            //     .Subscribe(index =>
            //     {
            //         var code = _languageValues[index];
            //         // var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
            //         // LocalizationSettings.SelectedLocale = locale;
            //         // _onLanguageChanged.OnNext(code);
            //     })
            //     .AddTo(Disposables);

            #endregion

            #region Camera

            _cameraControlHorizontal.SetIndex(0);
            _cameraControlHorizontal.OnValueChanged
                .Subscribe(index =>
                {
                    Debug.Log($"Camera Control Horizontal: {index}");
                }).AddTo(Disposables);

            _cameraControlVertical.SetIndex(0);
            _cameraControlVertical.OnValueChanged
                .Subscribe(index =>
                {
                    Debug.Log($"Camera Control Vertical: {index}");
                }).AddTo(Disposables);

            _cameraSensitivityHorizontal.OnValueChanged
                .Subscribe(value =>
                {
                    Debug.Log($"Camera Sensitivity Horizontal: {value}");
                }).AddTo(Disposables);

            _cameraSensitivityVertical.OnValueChanged
                .Subscribe(value =>
                {
                    Debug.Log($"Camera Sensitivity Vertical: {value}");
                }).AddTo(Disposables);

            _cameraAcceleration.OnValueChanged
                .Subscribe(value =>
                {
                    Debug.Log($"Camera Acceleration: {value}");
                }).AddTo(Disposables);

            _cameraShake.OnValueChanged
                .Subscribe(value =>
                {
                    Debug.Log($"Camera Shake: {value}");
                }).AddTo(Disposables);

            #endregion

            #region Video

            int displayModeIndex = 0;
            for (int i = 0; i < _displayModeValues.Count; i++)
            {
                var mode = _displayModeValues[i];
                if (mode == Screen.fullScreenMode)
                {
                    displayModeIndex = i;
                }
            }

            _displayMode.SetIndex(displayModeIndex);
            _displayMode.OnValueChanged
                .Subscribe(index =>
                {
                    var fullScreenMode = _displayModeValues[index];
                    var resolution = Screen.currentResolution;
                    Screen.SetResolution(resolution.width, resolution.height, fullScreenMode);
                    Debug.Log($"Option FullScreenMode: {fullScreenMode} => {_displayMode.GetLabel(index)}");
                })
                .AddTo(Disposables);

            int resolutionIndex = 0;
            for (int i = 0; i < _resolutionValues.Count; i++)
            {
                var resolution = _resolutionValues[i];
                Debug.Log($"Option Resolution: {resolution}");

                if (Screen.currentResolution.width == resolution.Width
                    && Screen.currentResolution.height == resolution.Height)
                {
                    resolutionIndex = i;
                }
            }

            _resolution.SetIndex(resolutionIndex);
            _resolution.OnValueChanged
                .Subscribe(index =>
                {
                    var resolution = _resolutionValues[index];
                    Screen.SetResolution(resolution.Width, resolution.Height, Screen.fullScreenMode);
                    Debug.Log($"Option Resolution: width={resolution.Width} height={resolution.Height}");
                })
                .AddTo(Disposables);

            _cameraFov.OnValueChanged
                .Subscribe(value =>
                {
                    Debug.Log($"Option Fov: {value}");
                })
                .AddTo(Disposables);

            #endregion

            #region Graphics

            _graphicQualityPreset.OnValueChanged
                .Subscribe(index =>
                {
                    var quality = _graphicQualityValues[index];
                    Debug.Log($"Option Graphics: {index} => {quality}");
                })
                .AddTo(Disposables);

            #endregion

            #region Audio

            // _masterVolume.OnValueChanged
            //     .Subscribe(value =>
            //     {
            //         Debug.Log($"Option Master Volume: {value}");
            //     })
            //     .AddTo(Disposables);
            //
            // _bgmVolume.OnValueChanged
            //     .Subscribe(value =>
            //     {
            //         Debug.Log($"Option BGM Volume: {value}");
            //     })
            //     .AddTo(Disposables);
            //
            // _voiceVolume.OnValueChanged
            //     .Subscribe(value =>
            //     {
            //         Debug.Log($"Option Voice Volume: {value}");
            //     })
            //     .AddTo(Disposables);
            //
            // _seVolume.OnValueChanged
            //     .Subscribe(value =>
            //     {
            //         Debug.Log($"Option SE Volume: {value}");
            //     })
            //     .AddTo(Disposables);

            #endregion
        }
    }

    public class HorrorOptionDialogModel : IDisposable
    {
        private bool _isDirty;

        private string _localCode;

        private float _masterVolume;
        private float _bgmVolume;
        private float _voiceVolume;
        private float _seVolume;

        public HorrorOptionDialogModel()
        {
            LoadSaveData();
        }

        private void LoadSaveData()
        {
        }

        public void SetLocalCode(string localCode)
        {
            if (_localCode == localCode) return;
            SetDirty();
            _localCode = localCode;
        }

        public void SetMasterVolume(float volume)
        {
            if (_masterVolume.Equals(volume)) return;
            SetDirty();
            _masterVolume = volume;
        }

        public void SetBgmVolume(float volume)
        {
            if (_bgmVolume.Equals(volume)) return;
            SetDirty();
            _bgmVolume = volume;
        }

        public void SetVoiceVolume(float volume)
        {
            if (_voiceVolume.Equals(volume)) return;
            SetDirty();
            _voiceVolume = volume;
        }

        public void SetSeVolume(float volume)
        {
            if (_seVolume.Equals(volume)) return;
            SetDirty();
            _seVolume = volume;
        }

        public bool IsDirty() => _isDirty;

        private void SetDirty() => _isDirty = true;

        public void Reset()
        {
            _isDirty = false;
        }

        public void Dispose()
        {
            if (_isDirty)
            {
                // Save
            }
        }
    }
}
