using Game.Shared.Scriptable.Database;
using UnityEditor;
using UnityEngine;

namespace Game.Shared.Scriptable.Database.EditorTools
{
    /// <summary>
    /// ScriptableDatabase.asset の Inspector に一括 CSV/TSV 入出力ボタンを表示する。
    /// 実処理は <see cref="ScriptableDatabaseIO"/> に委譲する（target を ScriptableObject として渡す）。
    /// </summary>
    [CustomEditor(typeof(ScriptableDatabase))]
    public class ScriptableDatabaseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var database = (ScriptableObject)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("一括 CSV / TSV", EditorStyles.boldLabel);

            EditorGUILayout.LabelField("Export", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Export All (CSV)")) ScriptableDatabaseIO.BatchExport(database, "csv");
                if (GUILayout.Button("Export All (TSV)")) ScriptableDatabaseIO.BatchExport(database, "tsv");
            }

            EditorGUILayout.LabelField("Import (Replace)", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Import All (CSV)")) ScriptableDatabaseIO.BatchImport(database, "csv", mergeByPrimaryKey: false);
                if (GUILayout.Button("Import All (TSV)")) ScriptableDatabaseIO.BatchImport(database, "tsv", mergeByPrimaryKey: false);
            }

            EditorGUILayout.LabelField("Import (Merge by PrimaryKey)", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Import All (CSV)")) ScriptableDatabaseIO.BatchImport(database, "csv", mergeByPrimaryKey: true);
                if (GUILayout.Button("Import All (TSV)")) ScriptableDatabaseIO.BatchImport(database, "tsv", mergeByPrimaryKey: true);
            }
        }
    }
}
