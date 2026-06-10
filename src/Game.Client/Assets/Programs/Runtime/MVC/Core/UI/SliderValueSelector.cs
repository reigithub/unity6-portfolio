using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.UI
{
    /// <summary>
    /// 最小値・最大値・刻み幅(step)を指定でき、現在値を右側テキストに反映するスライダー。
    /// 内部 Slider を wholeNumbers(整数) にし、value を「step インデックス(0..N)」として扱う。
    /// 実値 = _min + slider.value * _step。ゲームパッド左右はネイティブ stepSize=1 で実値 +step。
    /// </summary>
    public class SliderValueSelector : MonoBehaviour
    {
        [SerializeField] private Slider _slider;
        [SerializeField] private float _min;
        [SerializeField] private float _max = 100f;
        [SerializeField] private float _step = 5f;
        [SerializeField] private float _value;
        [SerializeField] private TextMeshProUGUI _valueText;
        [SerializeField] private string _valueTextFormat = "F1";

        private bool _initialized;

        private readonly Subject<float> _onValueChanged = new();
        public Observable<float> OnValueChanged => _onValueChanged.AsObservable();

        /// <summary>現在の実値（step インデックスから逆算）</summary>
        public float Value => GetValue();

        private void Start()
        {
            Initialize();
        }

        /// <summary>
        /// 内部スケーリング(0..N)の設定と購読を初期化する（値の適用は SetValue で別途行う）。
        /// </summary>
        public void Initialize()
        {
            if (_initialized) return;

            Configure();

            _slider.OnValueChangedAsObservable()
                .Subscribe(_ =>
                {
                    var value = GetValue();
                    UpdateValueText(value);
                    _onValueChanged.OnNext(value);
                })
                .AddTo(this);

            _initialized = true;
        }

        [ContextMenu("Configure Slider")]
        public void Configure()
        {
            // 購読時の即時発火やスライダー再スケーリングで _value が上書きされる前に初期値を退避
            var initialValue = _value;

            var notchCount = Mathf.Max(1, Mathf.RoundToInt((_max - _min) / _step));
            _slider.minValue = 0;
            _slider.maxValue = notchCount;
            _slider.wholeNumbers = true;

            _slider.SetValueWithoutNotify(Mathf.RoundToInt((initialValue - _min) / _step)); // Slider 側で [0, maxValue] にクランプ
            UpdateValueText(GetValue());
        }

        public float GetValue()
        {
            return _min + _slider.value * _step;
        }

        /// <summary>
        /// 外部から実値を設定する。
        /// </summary>
        public void SetValue(float value)
        {
            Initialize();
            _slider.value = Mathf.RoundToInt((value - _min) / _step); // Slider 側で [0, maxValue] にクランプ
            UpdateValueText(GetValue());
        }

        private void UpdateValueText(float value)
        {
            _value = value;

            if (_valueText != null)
                _valueText.text = value.ToString(_valueTextFormat);
        }

#if UNITY_EDITOR
        // エディタ時に _min と _max を step の倍数へ補正（最近傍）。
        // min, max とも step グリッドに snap し、割り切れない組み合わせを設定段階で解消する。
        private void OnValidate()
        {
            if (_step <= 0f) return;
            _min = Mathf.Round(_min / _step) * _step; // 例: min=1, step=3 → 0
            _max = Mathf.Round(_max / _step) * _step; // 例: max=100, step=3 → 99
            if (_max <= _min) _max = _min + _step;    // 退化ガード
        }
#endif
    }
}
