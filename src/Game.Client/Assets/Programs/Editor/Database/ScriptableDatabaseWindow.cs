using System;
using System.Linq;
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
        // 検証対象の選択肢。列挙にリフレクション走査を伴うため、OnGUI では作らず遅延構築して保持する。
        private Type[] _recordTypes;
        private string[] _recordTypeNames;
        private int _selectedRecordType;

        [MenuItem("Project/Database/ScriptableDatabaseWindow")]
        public static void Open() => GetWindow<ScriptableDatabaseWindow>("Scriptable Database");

        // ドメインリロード（再コンパイル）のたびに選択肢を作り直す。
        private void OnEnable() => ClearRecordTypes();

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

            if (GUILayout.Button("Validate All（構成＋全テーブル）"))
                ScriptableDatabaseIO.RunWithDatabase(db => ScriptableDatabaseValidationRunner.Run(db));

            EnsureRecordTypes();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_recordTypes.Length == 0))
                {
                    _selectedRecordType = EditorGUILayout.Popup(_selectedRecordType, _recordTypeNames);
                    if (GUILayout.Button("Validate Selected", GUILayout.Width(140)))
                    {
                        var recordType = _recordTypes[_selectedRecordType];
                        ScriptableDatabaseIO.RunWithDatabase(db => ScriptableDatabaseValidationRunner.Run(db, recordType));
                    }
                }

                if (GUILayout.Button("↻", GUILayout.Width(24))) ClearRecordTypes();
            }

            if (_recordTypes.Length == 0)
                EditorGUILayout.HelpBox("検証対象のテーブルがありません。Build / Register を実行してください。", MessageType.Info);
        }

        // 選択肢の構築では未生成・未登録を通知しない（OnGUI から呼ぶため）。実行時に呼び出し側が通知する。
        private void EnsureRecordTypes()
        {
            if (_recordTypes != null) return;

            _recordTypes = ScriptableDatabaseValidationRunner.RecordTypes(ScriptableDatabaseIO.LoadDatabaseOrNull()).ToArray();
            _recordTypeNames = _recordTypes.Select(t => t.Name).ToArray();
            _selectedRecordType = Mathf.Clamp(_selectedRecordType, 0, Mathf.Max(0, _recordTypes.Length - 1));
        }

        private void ClearRecordTypes()
        {
            _recordTypes = null;
            _recordTypeNames = null;
        }
    }
}
