using System.Linq;
using Game.Core.Services;
using Game.Shared.Input;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.UI
{
    [System.Serializable]
    public class TabInfo
    {
        [SerializeField] private Toggle _tab;                  // タブヘッダ Toggle（ToggleGroup に紐付け）
        [SerializeField] private GameObject _tabContent;      // タブコンテンツ Panel（ScrollView を含む）
        [SerializeField] private Selectable _firstSelectable; // タブ内の最初のフォーカス対象（Slider/Dropdown）

        public Toggle Tab => _tab;
        public GameObject TabContent => _tabContent;
        public Selectable FirstSelectable => _firstSelectable;
    }

    public class TabGroup : MonoBehaviour
    {
        [SerializeField] private TabInfo[] _tabs;

        private IInputSystemService _inputService;
        private IInputSystemService InputService => _inputService ??= GameServiceManager.Resolve<IInputSystemService>();

        private readonly Subject<int> _onTabChanged = new();
        public Observable<int> OnTabChanged => _onTabChanged.AsObservable();

        private int _currentTabIndex;

        public void Initialize()
        {
            for (int i = 0; i < _tabs.Length; i++)
            {
                int index = i;
                _tabs[i].Tab.isOn = index == 0;
                _tabs[i].Tab.OnValueChangedAsObservable()
                    .Where(isOn => isOn)
                    .Subscribe(_ => ChangeTab(index))
                    .AddTo(this);

                if (_tabs[i].TabContent != null)
                    _tabs[i].TabContent.SetActive(true);
            }
        }

        public void ChangeTab(int index)
        {
            _currentTabIndex = index;

            for (int i = 0; i < _tabs.Length; i++)
            {
                var info = _tabs[i];
                if (info.TabContent != null)
                    info.TabContent.SetActive(i == index);

                if (info.Tab.isOn)
                    info.Tab.targetGraphic.color = info.Tab.colors.selectedColor;
                else
                    info.Tab.targetGraphic.color = info.Tab.colors.normalColor;
            }

            // EventSystem フォーカス移動
            var first = _tabs[index].FirstSelectable;
            if (first != null && first.IsSelectable() && first.gameObject.activeInHierarchy)
            {
                InputService.SetSelectedGameObject(first.gameObject);
            }
            else
            {
                var tabContent = _tabs[index].TabContent;
                if (tabContent != null)
                {
                    var selectable = _tabs[index].TabContent.GetComponentsInChildren<Selectable>(false)
                        .FirstOrDefault(x => x.IsSelectable());
                    if (selectable != null) InputService.SetSelectedGameObject(selectable.gameObject);
                }
            }

            _onTabChanged.OnNext(index);
        }

        private void CycleTab(int delta)
        {
            if (_tabs.Length == 0) return;
            var next = ((_currentTabIndex + delta) % _tabs.Length + _tabs.Length) % _tabs.Length;
            if (_tabs[next].Tab != null)
                _tabs[next].Tab.isOn = true;
        }

        public void NextTab() => CycleTab(+1);

        public void PreviousTab() => CycleTab(-1);
    }
}
