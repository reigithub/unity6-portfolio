using System.Collections.Generic;
using R3;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using TMPro;
using UnityEditor;

namespace Game.Shared.Localization
{
    public class LocalizeDropdown : MonoBehaviour
    {
        [SerializeField] private List<LocalizedString> _dropdownOptions;

        private TMP_Dropdown _tmpDropdown;

        private void Awake()
        {
            if (_tmpDropdown == null) TryGetComponent(out _tmpDropdown);

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
            var tmpDropdownOptions = new List<TMP_Dropdown.OptionData>(_dropdownOptions.Count);
            foreach (var dropdownOption in _dropdownOptions)
            {
                tmpDropdownOptions.Add(new TMP_Dropdown.OptionData(dropdownOption.GetLocalizedString()));
            }
            _tmpDropdown.options = tmpDropdownOptions;
            _tmpDropdown.RefreshShownValue();
        }
    }

#if UNITY_EDITOR

    public abstract class AddLocalizeDropdown
    {
        [MenuItem("CONTEXT/TMP_Dropdown/Localize", false, 1)]
        private static void AddLocalizeComponent()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected != null)
            {
                selected.AddComponent<LocalizeDropdown>();
            }
        }
    }

#endif
}
