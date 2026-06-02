using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using TMPro;
using UnityEditor;

namespace Game.Shared.Localization
{
    public class LocalizeFontMaterial : MonoBehaviour
    {
        [SerializeField] private LocalizedMaterial _fontMaterial;

        private TextMeshProUGUI _tmp;

        private void Awake()
        {
            if (_tmp == null) TryGetComponent(out _tmp);

            LocalizationSettings.SelectedLocaleChanged += ChangedLocale;
        }

        private void OnDestroy()
        {
            LocalizationSettings.SelectedLocaleChanged -= ChangedLocale;
        }

        private void ChangedLocale(Locale newLocale)
        {
            ChangedLocaleAsync(newLocale).Forget();
        }

        private async UniTask ChangedLocaleAsync(Locale newLocale)
        {
            _tmp.fontSharedMaterial = await _fontMaterial.LoadAssetAsync().ToUniTask();
        }
    }

    public abstract class AddLocalizeFontMaterial
    {
        [MenuItem("CONTEXT/TMP_Text/Localize Font Material", false, 1)]
        private static void AddLocalizeComponent()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected != null)
            {
                selected.AddComponent<LocalizeDropdown>();
            }
        }
    }
}
