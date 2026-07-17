using System.Collections.Generic;
using Game.Core.UI;
using Game.Horror.SaveData;
using Game.MVC.Core.Scenes;
using Game.Shared.Constants;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Horror.Dialogs
{
    public class HorrorOptionDialogComponent : GameSceneComponent
    {
        #region SerializeField

        [SerializeField] private TabGroup _tabGroup;

        [Header("Options - Gameplay")]
        [SerializeField] private SliderIndexSelector _language;
        [SerializeField] private GenericValues<string> _languageValues;

        [SerializeField] private SliderBooleanSelector _cameraControlHorizontal;
        [SerializeField] private SliderBooleanSelector _cameraControlVertical;

        [SerializeField] private SliderValueSelector _cameraSensitivityHorizontal;
        [SerializeField] private SliderValueSelector _cameraSensitivityVertical;

        [SerializeField] private SliderValueSelector _cameraAcceleration;
        [SerializeField] private SliderValueSelector _cameraShake;
        [SerializeField] private SliderValueSelector _cameraFov;

        [Header("Options - Control")]
        [SerializeField] private SliderBooleanSelector _sprintMode;
        [SerializeField] private SliderBooleanSelector _crouchMode;

        [SerializeField] private InputActionRebindingView[] _rebindViews;
        [SerializeField] private Button _resetKeyboardBindingsButton;
        [SerializeField] private Button _resetGamepadBindingsButton;

        [Header("Options - Graphics")]
        [SerializeField] private SliderIndexSelector _displayMode;
        [SerializeField] private GenericValues<FullScreenMode> _displayModeValues;

        [SerializeField] private SliderIndexSelector _resolution;
        [SerializeField] private GenericValues<ResolutionInfo> _resolutionValues;

        [SerializeField] private SliderValueSelector _frameRate;
        [SerializeField] private SliderBooleanSelector _uncappedFrameRate;
        [SerializeField] private SliderBooleanSelector _showFrameRate;
        [SerializeField] private SliderBooleanSelector _vSync;

        [Header("Options - Audio")]
        [SerializeField] private SliderValueSelector _masterVolume;
        [SerializeField] private SliderValueSelector _bgmVolume;
        [SerializeField] private SliderValueSelector _voiceVolume;
        [SerializeField] private SliderValueSelector _seVolume;

        #endregion

        #region Options - Game

        public Observable<string> OnLanguageChanged => _language.OnValueChanged.Select(index => _languageValues[index]);

        public Observable<bool> OnCameraControlHorizontalChanged => _cameraControlHorizontal.OnValueChanged;
        public Observable<bool> OnCameraControlVerticalChanged => _cameraControlVertical.OnValueChanged;
        public Observable<float> OnCameraSensitivityHorizontalChanged => _cameraSensitivityHorizontal.OnValueChanged;
        public Observable<float> OnCameraSensitivityVerticalChanged => _cameraSensitivityVertical.OnValueChanged;
        public Observable<float> OnCameraAccelerationChanged => _cameraAcceleration.OnValueChanged;
        public Observable<float> OnCameraShakeChanged => _cameraShake.OnValueChanged;
        public Observable<float> OnCameraFovChanged => _cameraFov.OnValueChanged;

        #endregion

        #region Options - Graphics

        public Observable<bool> OnSprintModeChanged => _sprintMode.OnValueChanged;
        public Observable<bool> OnCrouchModeChanged => _crouchMode.OnValueChanged;

        public Observable<FullScreenMode> OnDisplayModeChanged => _displayMode.OnValueChanged.Select(index => _displayModeValues[index]);
        public Observable<ResolutionInfo> OnResolutionChanged => _resolution.OnValueChanged.Select(index => _resolutionValues[index]);
        public Observable<float> OnFrameRateChanged => _frameRate.OnValueChanged;
        public Observable<bool> OnUncappedFrameRateChanged => _uncappedFrameRate.OnValueChanged;
        public Observable<bool> OnShowFrameRateChanged => _showFrameRate.OnValueChanged;
        public Observable<bool> OnVSyncChanged => _vSync.OnValueChanged;

        #endregion

        #region Controls

        /// <summary>キーリバインド行（アクション×スキーム単位）。Dialog 側が購読・表示更新する。</summary>
        public IReadOnlyList<InputActionRebindingView> RebindViews => _rebindViews;

        /// <summary>スキーム別リセットボタン押下。値は対象スキーム（KBM / Gamepad）。</summary>
        public Observable<string> OnResetSchemeBindingsRequested => Observable.Merge(
            _resetKeyboardBindingsButton.OnClickAsObservable().Select(_ => InputControlSchemes.KeyboardAndMouse),
            _resetGamepadBindingsButton.OnClickAsObservable().Select(_ => InputControlSchemes.Gamepad));

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

            // Gameplay
            _language.SetIndex(_languageValues[d.LanguageCode]);
            _cameraControlHorizontal.SetBool(d.CameraControlHorizontal);
            _cameraControlVertical.SetBool(d.CameraControlVertical);
            _cameraSensitivityHorizontal.SetValue(d.CameraSensitivityHorizontal);
            _cameraSensitivityVertical.SetValue(d.CameraSensitivityVertical);
            _cameraAcceleration.SetValue(d.CameraAcceleration);
            _cameraShake.SetValue(d.CameraShake);
            _cameraFov.SetValue(d.CameraFov);

            _sprintMode.SetBool(d.SprintToggle);
            _crouchMode.SetBool(d.CrouchToggle);

            // Display
            _displayMode.SetIndex(_displayModeValues[d.DisplayMode]);
            _resolution.SetIndex(ResolveResolutionIndex(d.ResolutionWidth, d.ResolutionHeight));
            _frameRate.SetValue(d.FrameRateLimit);
            _uncappedFrameRate.SetBool(d.UncappedFrameRate);
            _showFrameRate.SetBool(d.ShowFrameRate);
            _vSync.SetBool(d.VSync);

            // Audio
            _masterVolume.SetValue(d.MasterVolume);
            _bgmVolume.SetValue(d.BgmVolume);
            _voiceVolume.SetValue(d.VoiceVolume);
            _seVolume.SetValue(d.SeVolume);

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
