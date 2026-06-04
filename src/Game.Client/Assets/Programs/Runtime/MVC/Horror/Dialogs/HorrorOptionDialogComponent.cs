using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Core.UI;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using Game.Shared.Enums;
using R3;
using R3.Triggers;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace Game.Horror.Dialogs
{
    public class HorrorOptionDialog : GameDialogScene<HorrorOptionDialog, HorrorOptionDialogComponent, bool>
    {
        protected override string AssetPathOrAddress => "HorrorOptionDialog";

        private InputSystemService _inputService;
        private InputSystemService InputService => _inputService ??= GameServiceManager.Get<InputSystemService>();

        public static async UniTask<bool> RunAsync()
        {
            var sceneService = GameServiceManager.Get<GameSceneService>();
            return await sceneService.TransitionDialogAsync<HorrorOptionDialog, bool>();
        }

        public override UniTask Startup()
        {
            SceneComponent.UpdateAsObservable()
                .Where(_ => State.IsProcessing())
                .Subscribe(_ =>
                {
                    // ダイアログキャンセル
                    if (InputService.UI.Cancel.WasPressedThisFrame() || InputService.UI.Menu.WasPressedThisFrame())
                    {
                        TrySetResult(default);
                        return;
                    }

                    // L1 (Previous) / R1 (Next) でタブ循環
                    if (InputService.UI.Previous.WasPressedThisFrame())
                        SceneComponent.PreviousTab();
                    else if (InputService.UI.Next.WasPressedThisFrame())
                        SceneComponent.NextTab();
                })
                .AddTo(Disposables);

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

        [Header("Options - Video")]
        [SerializeField] private SliderIndexSelector _displayMode;
        [SerializeField] private GenericValues<FullScreenMode> _displayModeValues;

        [SerializeField] private SliderIndexSelector _resolution;
        [SerializeField] private GenericValues<ResolutionInfo> _resolutionValues;

        [SerializeField] private SliderValueSelector _fov;

        [Header("Options - Graphics")]
        [SerializeField] private SliderIndexSelector _graphicsPreset;
        [SerializeField] private GenericValues<GraphicQuality> _graphicsPresetValues;

        [Header("Options - Audio")]
        [SerializeField] private SliderValueSelector _masterVolume;
        [SerializeField] private SliderValueSelector _bgmVolume;
        [SerializeField] private SliderValueSelector _voiceVolume;
        [SerializeField] private SliderValueSelector _seVolume;

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

            _language.OnValueChanged
                .Subscribe(index =>
                {
                    var code = _languageValues[index];
                    var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
                    LocalizationSettings.SelectedLocale = locale;
                })
                .AddTo(Disposables);

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

            _fov.OnValueChanged
                .Subscribe(value =>
                {
                    Debug.Log($"Option Fov: {value}");
                })
                .AddTo(Disposables);

            #endregion

            #region Graphics

            _graphicsPreset.OnValueChanged
                .Subscribe(index =>
                {
                    var quality = _graphicsPresetValues[index];
                    Debug.Log($"Option Graphics: {index} => {quality}");
                })
                .AddTo(Disposables);

            #endregion

            #region Audio

            _masterVolume.OnValueChanged
                .Subscribe(value =>
                {
                    Debug.Log($"Option Master Volume: {value}");
                })
                .AddTo(Disposables);

            _bgmVolume.OnValueChanged
                .Subscribe(value =>
                {
                    Debug.Log($"Option BGM Volume: {value}");
                })
                .AddTo(Disposables);

            _voiceVolume.OnValueChanged
                .Subscribe(value =>
                {
                    Debug.Log($"Option Voice Volume: {value}");
                })
                .AddTo(Disposables);

            _seVolume.OnValueChanged
                .Subscribe(value =>
                {
                    Debug.Log($"Option SE Volume: {value}");
                })
                .AddTo(Disposables);

            #endregion
        }
    }
}
