using Game.Shared.Scriptable.Database;
using UnityEditor;
using UnityEngine;

namespace Game.Shared.Scriptable.Database.EditorTools
{
    /// <summary>
    /// ScriptableTable 派生アセット共通の Inspector。
    /// 既定の描画に加え、未整列時の警告と「Sort & Validate」ボタンを表示する
    /// （整列は OnValidate 自動実行をやめ、ここから手動実行する）。
    /// </summary>
    [CustomEditor(typeof(ScriptableTableBase), editorForChildClasses: true)]
    public class ScriptableTableEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var table = (ScriptableTableBase)target;
            if (!table.EditorIsSorted())
            {
                EditorGUILayout.HelpBox(
                    "records が主キー昇順に整列されていません（空要素を含む場合あり）。実行時の検索が誤動作します。『Sort & Validate』を実行してください。",
                    MessageType.Warning);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Sort & Validate"))
            {
                foreach (var o in targets)
                {
                    var tb = (ScriptableTableBase)o;
                    Undo.RecordObject(tb, "Sort & Validate");
                    tb.EditorSortAndValidate();
                    EditorUtility.SetDirty(tb);
                }
            }
        }
    }
}
