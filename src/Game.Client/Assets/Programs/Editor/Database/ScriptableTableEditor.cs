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
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

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

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("CSV / TSV", EditorStyles.boldLabel);

            // Import は対象アセットが一意に定まる単一選択時のみ。
            if (targets.Length == 1)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Import (Replace)"))
                        ScriptableTableIO.Import(table, mergeByPrimaryKey: false);
                    if (GUILayout.Button("Import (Merge by PrimaryKey)"))
                        ScriptableTableIO.Import(table, mergeByPrimaryKey: true);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Import は単一アセット選択時のみ実行できます。", MessageType.Info);
            }

            EditorGUILayout.Space();

            // Export は複数選択でも各アセットを個別に書き出す。
            // SaveFilePanel は単一拡張子しか扱えないため形式ごとにボタンを分ける。
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Export to CSV"))
                {
                    foreach (var o in targets)
                        ScriptableTableIO.Export((ScriptableTableBase)o, "csv");
                }
                if (GUILayout.Button("Export to TSV"))
                {
                    foreach (var o in targets)
                        ScriptableTableIO.Export((ScriptableTableBase)o, "tsv");
                }
            }
        }
    }
}
