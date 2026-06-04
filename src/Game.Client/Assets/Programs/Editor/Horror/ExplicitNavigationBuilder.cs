using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor.MenuCommands
{
    /// <summary>
    /// 選択した GameObject の子階層にある Selectable に対して、
    /// Explicit Navigation（Up/Down 循環、Left/Right null）を一括設定する Editor ツール。
    ///
    /// 用途: Horror オプション画面のタブコンテンツ Panel に対して、Hierarchy 右クリック →
    ///       Horror / Build Tab Content Navigation を実行することで、
    ///       D-Pad U/D がタブ内のみで巡回しタブ Toggle へリークしない設定をエディタ時に焼き込む。
    /// </summary>
    public static class ExplicitNavigationBuilder
    {
        private const string MenuPath = "GameObject/Navigation/Build Explicit Navigation";

        // priority = 10 は GameObject/ メニューを Hierarchy 右クリックメニューに伝播させるために必須
        // 参照: https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MenuItem.html
        [MenuItem(MenuPath, false, 0)]
        private static void ExecCommand(MenuCommand command)
        {
            var target = command.context as GameObject;
            if (target == null) return;

            var selectables = target.GetComponentsInChildren<Selectable>(includeInactive: false)
                .Where(x => x.navigation.mode != Navigation.Mode.None)
                .ToArray();
            if (selectables.Length == 0)
            {
                Debug.LogWarning($"[HorrorTabContentNavigationBuilder] No Selectable found under '{target.name}'");
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

            Debug.Log($"[HorrorTabContentNavigationBuilder] Built Explicit Up/Down navigation for {selectables.Length} Selectables under '{target.name}'");
        }

        // Validate メソッドは引数なしが Unity 公式サンプルの形
        [MenuItem(MenuPath, true)]
        private static bool Validate()
        {
            return Selection.activeGameObject != null;
        }
    }
}
