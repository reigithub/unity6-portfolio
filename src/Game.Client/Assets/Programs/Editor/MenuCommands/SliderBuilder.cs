using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor.MenuCommands
{
    public static class SliderBuilder
    {
        private const string MenuPath = "GameObject/UI/Build Slider";

        [MenuItem(MenuPath, false, 0)]
        private static void ExecCommand(MenuCommand command)
        {
            var target = command.context as GameObject;
            if (target == null) return;

            var sliders = target.GetComponentsInChildren<Slider>(includeInactive: false)
                .Where(slider => slider.GetComponent<RectTransform>().rect.width > 0f)
                .ToArray();
            if (sliders.Length == 0)
            {
                Debug.LogWarning($"[SliderBuilder] No Sliders found under '{target.name}'");
                return;
            }

            Undo.RecordObjects(sliders, "Build Slider");

            for (int i = 0; i < sliders.Length; i++)
            {
                var colors = sliders[i].colors;
                colors.normalColor = new Color(colors.normalColor.r, colors.normalColor.g, colors.normalColor.b, 0f);
                var selected = new Color(0.59f, 0.59f, 0.59f, 0.099f);
                colors.highlightedColor = selected;
                colors.pressedColor = selected;
                colors.selectedColor = selected;
                colors.disabledColor = new Color(colors.disabledColor.r, colors.disabledColor.g, colors.disabledColor.b, 0f);
                sliders[i].colors = colors;

                var uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

                var fill = sliders[i].fillRect;
                if (fill.TryGetComponent<Image>(out var fillImage))
                {
                    fillImage.color = new Color(0f, 0.59f, 0.59f, 1f);
                    fillImage.sprite = uiSprite;
                }

                var handle = sliders[i].handleRect;
                handle.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 20f);
                if (handle.TryGetComponent<Image>(out var handleImage))
                {
                    handleImage.color = Color.white;
                    handleImage.sprite = uiSprite;
                }

                var handleParent = sliders[i].handleRect.parent;
                if (handleParent.TryGetComponent<RectTransform>(out var handleParentHandle))
                {
                    handleParentHandle.offsetMin = new Vector2(handleParentHandle.offsetMin.x, 5f);
                    handleParentHandle.offsetMax = new Vector2(handleParentHandle.offsetMax.x, -5f);
                }

                EditorUtility.SetDirty(sliders[i]);
            }

            Debug.Log($"[SliderBuilder] Built Slider for {sliders.Length} Slider under '{target.name}'");
        }

        [MenuItem(MenuPath, true)]
        private static bool Validate()
        {
            return Selection.activeGameObject != null;
        }
    }
}
