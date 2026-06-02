using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Core.UI;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using R3;
using R3.Triggers;
using TMPro;
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
                    if (InputService.UI.Menu.WasPressedThisFrame())
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

        [SerializeField] private TMP_Dropdown _language;
        [SerializeField] private DropdownValues<string> _languageValues;

        [SerializeField] private TMP_Dropdown _displayMode;
        [SerializeField] private DropdownValues<FullScreenMode> _displayModeValues;

        [SerializeField] private TMP_Dropdown _resolution;
        [SerializeField] private DropdownValues<ResolutionInfo> _resolutionValues;

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
#if UNITY_EDITOR
            foreach (var locale in LocalizationSettings.AvailableLocales.Locales)
            {
                Debug.Log($"Localization Code: {locale.Identifier.Code} / LocalName: {locale.LocaleName}");
            }
#endif

            _language.OnValueChangedAsObservable()
                .Subscribe(index =>
                {
                    var code = _languageValues[index];
                    var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
                    LocalizationSettings.SelectedLocale = locale;
                })
                .AddTo(Disposables);

            int displayModeIndex = 0;
            for (int i = 0; i < _displayModeValues.Count; i++)
            {
                var mode = _displayModeValues[i];
                if (mode == Screen.fullScreenMode)
                {
                    displayModeIndex = i;
                }
            }

            _displayMode.value = displayModeIndex;
            _displayMode.OnValueChangedAsObservable()
                .Subscribe(index =>
                {
                    var fullScreenMode = _displayModeValues[index];
                    var resolution = Screen.currentResolution;
                    Screen.SetResolution(resolution.width, resolution.height, fullScreenMode);
                    Debug.Log($"Option FullScreenMode: {fullScreenMode} => {_displayMode.options[index].text}");
                })
                .AddTo(Disposables);

            int resolutionIndex = 0;
            for (int i = 0; i < _resolutionValues.Count; i++)
            {
                var resolution = _resolutionValues[i];
                Debug.Log($"Option Resolution: {resolution.Width} x {resolution.Height}");

                if (Screen.currentResolution.width == resolution.Width
                    && Screen.currentResolution.height == resolution.Height)
                {
                    resolutionIndex = i;
                }
            }

            _resolution.value = resolutionIndex;
            _resolution.OnValueChangedAsObservable()
                .Subscribe(index =>
                {
                    var resolution = _resolutionValues[index];
                    Screen.SetResolution(resolution.Width, resolution.Height, Screen.fullScreenMode);
                    Debug.Log($"Option Resolution: width={resolution.Width} height={resolution.Height}");
                })
                .AddTo(Disposables);
        }
    }
}
