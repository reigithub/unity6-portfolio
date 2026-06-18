using R3;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Game.Shared.Localization
{
    public class LocalizeStrings : MonoBehaviour
    {
        [SerializeField] private LocalizedString[] _localizedStrings;

        private string[] _strings;

        private readonly Subject<string[]> _onChangedLocale = new();
        public Observable<string[]> OnChangedLocale => _onChangedLocale.AsObservable();

        private void Awake()
        {
            LocalizationEvents.OnLocaleChanged.Subscribe(x => OnLocaleChanged(x)).AddTo(this);
        }

        private void OnEnable()
        {
            UpdateLocale();
        }

        [ContextMenu("Update Locale")]
        public void UpdateLocale()
        {
            OnLocaleChanged(LocalizationSettings.SelectedLocale);
        }

        private void OnLocaleChanged(Locale newLocale)
        {
            _strings = new string[_localizedStrings.Length];

            for (int i = 0; i < _strings.Length; i++)
            {
                _strings[i] = _localizedStrings[i].GetLocalizedString();
            }

            _onChangedLocale.OnNext(_strings);
        }
    }
}
