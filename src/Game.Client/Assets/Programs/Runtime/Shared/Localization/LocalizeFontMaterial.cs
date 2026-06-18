using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.Localization;
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

            LocalizationEvents.OnLocaleChanged.Subscribe(x => OnLocaleChanged(x)).AddTo(this);
        }

        private void OnLocaleChanged(Locale newLocale)
        {
            OnLocaleChangedAsync(newLocale).Forget();
        }

        private async UniTask OnLocaleChangedAsync(Locale newLocale)
        {
            _tmp.fontSharedMaterial = await _fontMaterial.LoadAssetAsync().ToUniTask();
        }
    }

#if UNITY_EDITOR

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

#endif
}
