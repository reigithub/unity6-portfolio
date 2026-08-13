using UnityEditor;
using UnityEngine;

namespace Game.Shared.Scriptable.Database.EditorTools
{
    /// <summary>
    /// Scriptable Database の生成・登録・一括 CSV/TSV 入出力・検証を集約したエディタウィンドウ。
    /// 各ボタンは既存の static コマンド（Generator / Builder / IO / ValidationRunner）を呼ぶだけで、ロジックは持たない。
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

            EditorGUILayout.Space();
            DrawValidation();
        }

        private void DrawValidation()
        {
            EditorGUILayout.LabelField("検証", EditorStyles.boldLabel);

            if (GUILayout.Button("Validate All（構成＋全テーブル。結果は Console）"))
                ScriptableDatabaseIO.RunWithDatabase(db => ScriptableDatabaseValidationRunner.Run(db));

            // テーブル単位の実行と結果の閲覧は専用ウィンドウが担う。
            if (GUILayout.Button("検証ウィンドウを開く（テーブル別の結果・エラー詳細）"))
                ScriptableDatabaseValidationWindow.Open();
        }
    }
}
