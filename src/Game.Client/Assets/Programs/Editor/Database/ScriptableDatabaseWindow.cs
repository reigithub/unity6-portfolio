using UnityEditor;
using UnityEngine;

namespace Game.Shared.Scriptable.Database.EditorTools
{
    /// <summary>
    /// Scriptable Database の生成・登録・一括 CSV/TSV 入出力を集約したエディタウィンドウ。
    /// 各ボタンは既存の static コマンド（Generator / Builder / IO）を呼ぶだけで、ロジックは持たない。
    /// </summary>
    public class ScriptableDatabaseWindow : EditorWindow
    {
        [MenuItem("Project/Database/ScriptableDatabaseWindow")]
        public static void Open() => GetWindow<ScriptableDatabaseWindow>("Scriptable Database");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("コード生成", EditorStyles.boldLabel);
            if (GUILayout.Button("Generate（テーブルクラス {Type}Table.g.cs）")) ScriptableTableGenerator.Generate();
            if (GUILayout.Button("Build（コンテナクラス ScriptableDatabase.g.cs）")) ScriptableDatabaseBuilder.Build();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("資産登録", EditorStyles.boldLabel);
            if (GUILayout.Button("Register（テーブル資産を .asset へ結線）")) ScriptableDatabaseBuilder.Register();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("一括 Export", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Export All (CSV)"))
                    ScriptableDatabaseIO.RunWithDatabase(db => ScriptableDatabaseIO.BatchExport(db, "csv"));
                if (GUILayout.Button("Export All (TSV)"))
                    ScriptableDatabaseIO.RunWithDatabase(db => ScriptableDatabaseIO.BatchExport(db, "tsv"));
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("一括 Import (Replace)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Import All (CSV)"))
                    ScriptableDatabaseIO.RunWithDatabase(db => ScriptableDatabaseIO.BatchImport(db, "csv", mergeByPrimaryKey: false));
                if (GUILayout.Button("Import All (TSV)"))
                    ScriptableDatabaseIO.RunWithDatabase(db => ScriptableDatabaseIO.BatchImport(db, "tsv", mergeByPrimaryKey: false));
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("一括 Import (Merge by PrimaryKey)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Import All (CSV)"))
                    ScriptableDatabaseIO.RunWithDatabase(db => ScriptableDatabaseIO.BatchImport(db, "csv", mergeByPrimaryKey: true));
                if (GUILayout.Button("Import All (TSV)"))
                    ScriptableDatabaseIO.RunWithDatabase(db => ScriptableDatabaseIO.BatchImport(db, "tsv", mergeByPrimaryKey: true));
            }
        }
    }
}
