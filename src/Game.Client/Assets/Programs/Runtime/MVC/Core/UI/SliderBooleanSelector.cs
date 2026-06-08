using Game.Shared.Localization;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.UI
{
    /// <summary>ON/OFF の2値を 0/1 スライダーで扱うセレクタ。&lt;/&gt; とゲームパッド左右でトグルする。</summary>
    public class SliderBooleanSelector : MonoBehaviour
    {
        [SerializeField] private Slider _slider;
        [SerializeField] private Button _prevButton;   // < → OFF(0)
        [SerializeField] private Button _nextButton;   // > → ON(1)
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private string[] _labels;     // [off, on]
        [SerializeField] private LocalizeStrings _localizeStrings; // 任意: [off, on] の2エントリ

        private bool _initialized;

        private readonly Subject<bool> _onValueChanged = new();
        public Observable<bool> OnValueChanged => _onValueChanged.AsObservable();

        /// <summary>現在の ON/OFF。</summary>
        public bool IsOn => _slider.value > 0.5f;

        private void Start() => Initialize();

        /// <summary>スライダー設定（0..1）とボタン配線・購読を初期化する（値の適用は SetValue で別途行う）。</summary>
        public void Initialize()
        {
            if (_initialized) return;

            Configure();

            _prevButton.OnClickAsObservable().Subscribe(_ => _slider.value = 0).AddTo(this);
            _nextButton.OnClickAsObservable().Subscribe(_ => _slider.value = 1).AddTo(this);

            // Gamepad（左右）・ボタンクリックのどちらの値変更もここに集約される。
            _slider.OnValueChangedAsObservable()
                .Subscribe(_ =>
                {
                    Refresh();
                    _onValueChanged.OnNext(IsOn);
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

        /// <summary>外部から値を設定する（SliderIndexSelector.SetIndex と同じく _slider.value 経由で通知あり）。</summary>
        public void SetBool(bool isOn)
        {
            Initialize();
            _slider.value = isOn ? 1 : 0;
            Refresh();
        }

        /// <summary>ラベルを差し替える（[off, on] の2要素想定）。</summary>
        public void SetLabels(string[] labels)
        {
            _labels = labels;
            Refresh();
        }

        private void Configure()
        {
            _slider.wholeNumbers = true;
            _slider.minValue = 0;
            _slider.maxValue = 1;
        }

        private void Refresh()
        {
            if (_label != null && _labels is { Length: >= 2 })
                _label.text = _labels[IsOn ? 1 : 0];

            _prevButton.interactable = IsOn;    // ON のとき OFF へ戻せる
            _nextButton.interactable = !IsOn;   // OFF のとき ON へ進める
        }
    }
}
