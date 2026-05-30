using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using Game.Shared.Input;
using R3;
using R3.Triggers;
using UnityEngine;
using UnityEngine.UI;

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
                .Subscribe(_ =>
                {
                    if (FocusState is GameSceneFocusState.Unfocused)
                        return;

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

        public override UniTask Terminate()
        {
            return base.Terminate();
        }
    }

    public class HorrorOptionDialogComponent : GameSceneComponent
    {
        [System.Serializable]
        public class TabEntry
        {
            [SerializeField] public Toggle TabToggle;           // タブヘッダ Toggle（ToggleGroup に紐付け）
            [SerializeField] public GameObject TabContent;      // タブコンテンツ Panel（ScrollView を含む）
            [SerializeField] public Selectable FirstSelectable; // タブ内の最初のフォーカス対象（Slider/Dropdown）
        }

        [SerializeField] private ToggleGroup _tabGroup;
        [SerializeField] private TabEntry[] _tabs = new TabEntry[4];

        private readonly ReactiveProperty<int> _currentTabIndex = new(0);

        private InputSystemService _inputService;
        private InputSystemService InputService => _inputService ??= GameServiceManager.Get<InputSystemService>();

        public override async UniTask Startup()
        {
            for (int i = 0; i < _tabs.Length; i++)
            {
                int capturedIndex = i;
                var tab = _tabs[i];
                if (tab.TabToggle == null) continue;

                // タブ Toggle は D-Pad ナビゲーションの起点・終点にしない（クリック・L1/R1 のみ）
                var nav = tab.TabToggle.navigation;
                nav.mode = Navigation.Mode.None;
                tab.TabToggle.navigation = nav;

                // 同 ToggleGroup へ紐付け（Inspector 設定済でも保険として）
                tab.TabToggle.group = _tabGroup;

                // 状態変化（false → true）のみ反応
                tab.TabToggle.OnValueChangedAsObservable()
                    .Subscribe(isOn =>
                    {
                        if (isOn) ApplyTab(capturedIndex);
                    })
                    .AddTo(Disposables);
            }

            ApplyTab(0);

            await base.Startup();
        }

        /// <summary>
        /// L1/R1 で呼ばれる外部 API。Toggle.isOn 経由で ApplyTab を間接起動。
        /// </summary>
        public void CycleTab(int delta)
        {
            if (_tabs.Length == 0) return;
            var next = ((_currentTabIndex.Value + delta) % _tabs.Length + _tabs.Length) % _tabs.Length;
            if (_tabs[next].TabToggle != null)
                _tabs[next].TabToggle.isOn = true;
        }

        /// <summary>
        /// Toggle の onValueChanged から呼ばれる、実際のタブ表示・フォーカス・Navigation 構築処理。
        /// </summary>
        private void ApplyTab(int index)
        {
            _currentTabIndex.Value = index;
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
