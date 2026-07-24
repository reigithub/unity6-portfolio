using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor.MenuCommands
{
    /// <summary>
    /// 選択した GameObject の子階層にある Selectable に対して、 WrapAround Navigationを一括設定する Editor ツール。
    /// </summary>
    public static class WrapAroundNavigationBuilder
    {
        private const string MenuPathRoot = "GameObject/Navigation/WrapAround/";

        [MenuItem(MenuPathRoot + "Build Navigation(Horizontal)", false, 0)]
        private static void BuildHorizontal(MenuCommand command)
        {
            var target = command.context as GameObject;
            if (target == null) return;

            var selectables = CollectSelectables(target);
            if (selectables.Length == 0) return;

            Undo.RecordObjects(selectables, "Build Horizontal Navigation");

            for (int i = 0; i < selectables.Length; i++)
            {
                Apply(selectables[i], Navigation.Mode.Horizontal);
            }

            Debug.Log($"[NavigationBuilder] Built WrapAround Left/Right navigation for {selectables.Length} Selectables under '{target.name}'");
        }

        [MenuItem(MenuPathRoot + "Build Navigation(Vertical)", false, 0)]
        private static void BuildVertical(MenuCommand command)
        {
            var target = command.context as GameObject;
            if (target == null) return;

            var selectables = CollectSelectables(target);
            if (selectables.Length == 0) return;

            Undo.RecordObjects(selectables, "Build Vertical Navigation");

            for (int i = 0; i < selectables.Length; i++)
            {
                Apply(selectables[i], Navigation.Mode.Vertical);
            }

            Debug.Log($"[ExplicitNavigationBuilder] Built WrapAround Up/Down navigation for {selectables.Length} Selectables under '{target.name}'");
        }

        [MenuItem(MenuPathRoot, true)]
        private static bool Validate()
        {
            return Selection.activeGameObject != null;
        }

        /// <summary>
        /// 対象配下から Explicit 設定対象となる Selectable を収集する。Navigation.Mode.None は除外。
        /// 見つからなければ警告を出して空配列を返す。
        /// </summary>
        private static Selectable[] CollectSelectables(GameObject target)
        {
            var selectables = target.GetComponentsInChildren<Selectable>(includeInactive: false)
                .Where(x => x.navigation.mode != Navigation.Mode.None)
                .ToArray();
            if (selectables.Length == 0)
            {
                Debug.LogWarning($"[ExplicitNavigationBuilder] No Selectable found under '{target.name}'");
            }

            return selectables;
        }

        private static void Apply(Selectable selectable, Navigation.Mode mode)
        {
            var nav = selectable.navigation;
            nav.mode = mode;
            nav.selectOnUp = null;
            nav.selectOnDown = null;
            nav.selectOnLeft = null;
            nav.selectOnRight = null;
            nav.wrapAround = true;
            selectable.navigation = nav;
            EditorUtility.SetDirty(selectable);
        }
    }
}
