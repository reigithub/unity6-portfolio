using Game.Shared.Localization;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.UI
{
    public class SliderIndexSelector : MonoBehaviour
    {
        [SerializeField] private Slider _slider;
        [SerializeField] private Button _prevButton;
        [SerializeField] private Button _nextButton;
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private string[] _labels;
        [SerializeField] private LocalizeStrings _localizeStrings;

        private bool _initialized;

        private readonly Subject<int> _onValueChanged = new();
        public Observable<int> OnValueChanged => _onValueChanged;

        /// <summary>選択中の index。</summary>
        public int Index => Mathf.RoundToInt(_slider.value);

        public int Count => _labels?.Length ?? 0;

        private void Start()
        {
            Initialize();
        }

        /// <summary>
        /// スライダー設定とボタン配線・購読を初期化する（値の適用は SetValue で別途行う）。
        /// </summary>
        public void Initialize()
        {
            if (_initialized) return;

            Configure();

            _prevButton.OnClickAsObservable().Subscribe(_ => Step(-1)).AddTo(this);
            _nextButton.OnClickAsObservable().Subscribe(_ => Step(+1)).AddTo(this);

            // Gamepad（左右）・ボタンクリックのどちらの値変更もここに集約される。
            _slider.OnValueChangedAsObservable()
                .Subscribe(_ =>
                {
                    Refresh();
                    _onValueChanged.OnNext(Index);
                })
                .AddTo(this);

            if (_localizeStrings != null)
            {
                _localizeStrings.OnChangedLocale
                    .Subscribe(SetLabels)
                    .AddTo(this);
                _localizeStrings.UpdateLocale();
            }
            else
            {
                Refresh();
            }

            _initialized = true;
        }

        public string GetLabel(int index)
        {
            if (index < 0 || _labels == null || index >= _labels.Length)
                return "";

            return _labels[index];
        }

        /// <summary>選択肢ラベルを差し替える（resolution 等の動的な選択肢向け）。</summary>
        public void SetLabels(string[] labels)
        {
            _labels = labels;
            Configure();
            Refresh();
        }

        /// <summary>
        /// 外部から index を設定する。
        /// </summary>
        public void SetIndex(int index)
        {
            Initialize();
            _slider.value = Mathf.Clamp(index, 0, Mathf.Max(0, Count - 1));
            Refresh();
        }

        private void Configure()
        {
            _slider.wholeNumbers = true;
            _slider.minValue = 0;
            _slider.maxValue = Mathf.Max(0, Count - 1);
        }

        // Slider 値を ±1。OnValueChanged 経由で Refresh + 通知に集約する（端では clamp で値が変わらず発火しない）。
        private void Step(int dir)
        {
            _slider.value = Mathf.Clamp(Index + dir, 0, Mathf.Max(0, Count - 1));
        }

        private void Refresh()
        {
            if (_label != null && Count > 0)
                _label.text = _labels[Index];

            _prevButton.interactable = Index > 0;          // 左送り不可なら < を無効
            _nextButton.interactable = Index < Count - 1;  // 右送り不可なら > を無効
        }
    }
}
