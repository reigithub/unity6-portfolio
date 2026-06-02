using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Core.UI;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using Game.Shared.Input;
using R3;
using R3.Triggers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
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
                        SceneComponent.CycleTab(-1);
                    else if (InputService.UI.Next.WasPressedThisFrame())
                        SceneComponent.CycleTab(+1);
                })
                .AddTo(Disposables);

            return base.Startup();
        }
    }

    public class HorrorOptionDialogComponent : GameSceneComponent
    {
        #region SerializeField

        [System.Serializable]
        public class TabEntry
        {
            [SerializeField] public Toggle TabToggle;           // タブヘッダ Toggle（ToggleGroup に紐付け）
            [SerializeField] public GameObject TabContent;      // タブコンテンツ Panel（ScrollView を含む）
            [SerializeField] public Selectable FirstSelectable; // タブ内の最初のフォーカス対象（Slider/Dropdown）
        }

        [SerializeField] private ToggleGroup _tabGroup;
        [SerializeField] private TabEntry[] _tabs = new TabEntry[4];

        [SerializeField] private TMP_Dropdown _language;
        [SerializeField] private DropdownValues<string> _languageValues;

        [SerializeField] private TMP_Dropdown _displayMode;
        [SerializeField] private DropdownValues<FullScreenMode> _displayModeValues;

        [SerializeField] private TMP_Dropdown _resolution;
        [SerializeField] private DropdownValues<ResolutionInfo> _resolutionValues;

        #endregion

        #region Variables

        private InputSystemService _inputService;
        private InputSystemService InputService => _inputService ??= GameServiceManager.Get<InputSystemService>();

        private int _currentTabIndex;

        #endregion

        public override async UniTask Startup()
        {
            for (int i = 0; i < _tabs.Length; i++)
            {
                int index = i;
                var tab = _tabs[i];
                if (tab.TabToggle != null)
                {
                    // 状態変化（false → true）のみ反応
                    tab.TabToggle.OnValueChangedAsObservable()
                        .Where(isOn => isOn)
                        .Subscribe(_ => ApplyTab(index))
                        .AddTo(Disposables);
                }

                if (tab.TabContent != null)
                    tab.TabContent.SetActive(true);
            }

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

            _displayMode.OnValueChangedAsObservable()
                .Subscribe(index =>
                {
                    var fullScreenMode = _displayModeValues[index];
                    var resolution = Screen.currentResolution;
                    Screen.SetResolution(resolution.width, resolution.height, fullScreenMode);
                    Debug.Log($"Option FullScreenMode: {fullScreenMode} => {_displayMode.options[index].text}");
                })
                .AddTo(Disposables);

            _resolution.OnValueChangedAsObservable()
                .Subscribe(index =>
                {
                    var resolution = _resolutionValues[index];
                    Screen.SetResolution(resolution.Width, resolution.Height, Screen.fullScreenMode);
                    Debug.Log($"Option Resolution: width={resolution.Width} height={resolution.Height}");
                })
                .AddTo(Disposables);

            ApplyTab(0);

            await base.Startup();
        }

        public void CycleTab(int delta)
        {
            if (_tabs.Length == 0) return;
            var next = ((_currentTabIndex + delta) % _tabs.Length + _tabs.Length) % _tabs.Length;
            if (_tabs[next].TabToggle != null)
                _tabs[next].TabToggle.isOn = true;
        }

        private void ApplyTab(int index)
        {
            _currentTabIndex = index;
            for (int i = 0; i < _tabs.Length; i++)
            {
                var tab = _tabs[i];
                if (tab.TabContent != null)
                    tab.TabContent.SetActive(i == index);

                if (tab.TabToggle.isOn)
                    tab.TabToggle.targetGraphic.color = tab.TabToggle.colors.selectedColor;
                else
                    tab.TabToggle.targetGraphic.color = tab.TabToggle.colors.normalColor;
            }

            // EventSystem フォーカス移動
            var first = _tabs[index].FirstSelectable;
            if (first != null && first.IsSelectable())
            {
                InputService.SetSelectedGameObject(first.gameObject);
            }
        }

    }
}
