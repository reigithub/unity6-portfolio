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

            SceneComponent.Initialize(_model.Data);

            SceneComponent.OnLanguageChanged
                .Subscribe(code => { _model.SetLanguageCode(code); })
                .AddTo(Disposables);

            SceneComponent.OnDisplayModeChanged
                .Subscribe(mode => { _model.SetDisplayMode(mode);  })
                .AddTo(Disposables);
            SceneComponent.OnResolutionChanged
                .Subscribe(res => { _model.SetResolution(res.Width, res.Height); })
                .AddTo(Disposables);
            SceneComponent.OnFrameRateChanged
                .Subscribe(fps => { _model.SetFrameRateLimit(fps); })
                .AddTo(Disposables);
            SceneComponent.OnUncappedFrameRateChanged
                .Subscribe(b => { _model.SetUncappedFrameRate(b); })
                .AddTo(Disposables);
            SceneComponent.OnVSyncChanged
                .Subscribe(b => { _model.SetVSync(b);})
                .AddTo(Disposables);

            return base.Startup();
        }

        public override async UniTask Terminate()
        {
            await _model.SaveIfDirtyAsync();
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

        [SerializeField] private SliderValueSelector _frameRate;
        [SerializeField] private SliderIndexSelector _uncappedFrameRate;

        [SerializeField] private SliderIndexSelector _vSync;
        [SerializeField] private SliderIndexSelector _motionBlur;

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
        public Observable<float> OnCameraFovChanged => _cameraFov.OnValueChanged;

        #endregion

        #region Options - Video

        public Observable<FullScreenMode> OnDisplayModeChanged => _displayMode.OnValueChanged.Select(index => _displayModeValues[index]);
        public Observable<ResolutionInfo> OnResolutionChanged => _resolution.OnValueChanged.Select(index => _resolutionValues[index]);
        public Observable<float> OnFrameRateChanged => _frameRate.OnValueChanged;
        public Observable<bool> OnUncappedFrameRateChanged => _uncappedFrameRate.OnValueChanged.Select(index => index != 0);
        public Observable<bool> OnVSyncChanged => _vSync.OnValueChanged.Select(index => index != 0);
        public Observable<bool> OnMotionBlurChanged => _motionBlur.OnValueChanged.Select(index => index != 0);

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

        public void Initialize(HorrorOptionSaveData d)
        {
            _tabGroup.Initialize();

            _language.SetIndex(_languageValues[d.LanguageCode]);
            _displayMode.SetIndex(_displayModeValues[d.DisplayMode]);
            _resolution.SetIndex(ResolveResolutionIndex(d.ResolutionWidth, d.ResolutionHeight));
            _frameRate.SetValue(d.FrameRateLimit);
            _uncappedFrameRate.SetIndex(d.UncappedFrameRate ? 1 : 0);
            _vSync.SetIndex(d.VSync ? 1 : 0);

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

    public class HorrorOptionDialogModel
    {
        private readonly HorrorOptionSaveService _saveService;
        public HorrorOptionSaveData Data => _saveService.Data;

        public HorrorOptionDialogModel()
        {
            _saveService = GameServiceManager.Resolve<HorrorOptionSaveService>();
        }

        public void SetLanguageCode(string code)
        {
            _saveService.SetLanguageCode(code);
            HorrorOptionHelper.ApplyLanguage(code);
        }

        public void SetDisplayMode(FullScreenMode mode)
        {
            _saveService.SetDisplayMode(mode);
            HorrorOptionHelper.ApplyResolution(Data.DisplayMode, Data.ResolutionWidth, Data.ResolutionHeight);
        }

        public void SetResolution(int width, int height)
        {
            _saveService.SetResolution(width, height);
            HorrorOptionHelper.ApplyResolution(Data.DisplayMode, Data.ResolutionWidth, Data.ResolutionHeight);
        }

        public void SetFrameRateLimit(float fps)
        {
            _saveService.SetFrameRateLimit(Mathf.RoundToInt(fps));
            HorrorOptionHelper.ApplyFrameRate(Data.VSync, Data.UncappedFrameRate, Data.FrameRateLimit);
        }

        public void SetUncappedFrameRate(bool uncapped)
        {
            _saveService.SetUncappedFrameRate(uncapped);
            HorrorOptionHelper.ApplyFrameRate(Data.VSync, Data.UncappedFrameRate, Data.FrameRateLimit);
        }

        public void SetVSync(bool enabled)
        {
            _saveService.SetVSync(enabled);
            HorrorOptionHelper.ApplyFrameRate(Data.VSync, Data.UncappedFrameRate, Data.FrameRateLimit);
        }

        public UniTask SaveIfDirtyAsync()
        {
            return _saveService.SaveIfDirtyAsync();
        }
    }
}
