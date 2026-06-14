using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor.MenuCommands
{
    /// <summary>
    /// 選択した GameObject の子階層にある Selectable に対して、 Explicit Navigationを一括設定する Editor ツール。
    /// </summary>
    public static class ExplicitNavigationBuilder
    {
        private const string MenuPathRoot = "GameObject/Navigation/Explicit/";

        // priority = 0 は GameObject/ メニューを Hierarchy 右クリックメニューに伝播させるために必須
        // 参照: https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MenuItem.html
        [MenuItem(MenuPathRoot + "Build Navigation(Vertical)", false, 0)]
        private static void BuildVerticalOnly(MenuCommand command)
        {
            var target = command.context as GameObject;
            if (target == null) return;

            var selectables = target.GetComponentsInChildren<Selectable>(includeInactive: false)
                .Where(x => x.navigation.mode != Navigation.Mode.None)
                .ToArray();
            if (selectables.Length == 0)
            {
                Debug.LogWarning($"[ExplicitNavigationBuilder] No Selectable found under '{target.name}'");
                return;
            }

            Undo.RecordObjects(selectables, "Build Tab Content Navigation");

            for (int i = 0; i < selectables.Length; i++)
            {
                var nav = selectables[i].navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = i > 0 ? selectables[i - 1] : selectables[selectables.Length - 1];
                nav.selectOnDown = i < selectables.Length - 1 ? selectables[i + 1] : selectables[0];
                nav.selectOnLeft = null;
                nav.selectOnRight = null;
                selectables[i].navigation = nav;
                EditorUtility.SetDirty(selectables[i]);
            }

            Debug.Log($"[ExplicitNavigationBuilder] Built Explicit Up/Down navigation for {selectables.Length} Selectables under '{target.name}'");
        }

        [MenuItem(MenuPathRoot, true)]
        private static bool Validate()
        {
            return Selection.activeGameObject != null;
        }
    }
}
