using System;
using DG.Tweening;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.UI
{
    [Obsolete]
    public class SliderValueIndicator : MonoBehaviour
    {
        [SerializeField] private Slider _slider;
        [SerializeField] private Button[] _scales;
        [SerializeField] private Color _enableColor = Color.darkCyan;
        [SerializeField] private Color _disableColor = Color.white;

        private bool _initialized;

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_initialized) return;

            _slider.OnValueChangedAsObservable()
                .Subscribe(value =>
                {
                    var index = (int)value;
                    for (int i = 0; i < _scales.Length; i++)
                    {
                        if (i < index)
                            _scales[i].targetGraphic.DOColor(_enableColor, 0.3f).SetUpdate(true);
                        else
                            _scales[i].targetGraphic.DOColor(_disableColor, 0.3f).SetUpdate(true);
                    }
                })
                .AddTo(this);

            for (int i = 0; i < _scales.Length; i++)
            {
                var value = i + 1;
                _scales[i].OnClickAsObservable()
                    .Subscribe(_ => _slider.value = value)
                    .AddTo(this);
            }

            _initialized = true;
        }
    }
}
