using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Game.Core.MasterData;
using UnityEditor;
using UnityEngine;

namespace Game.MasterData.Editor
{
    public class MasterDataWindow : EditorWindow
    {
        // 1.MemoryTable手動作成
        // 2.MemoryTableからTsv新規作成（仮データ）または更新
        // 3.Tsvからマスターデータバイナリ作成
        // 4.Addressableに登録
        // 5.Addressable経由でバイナリをロードしてデータベース構築（⇒MasterDataServiceでアプリ起動時に読み込む）
        // EX.MySql定義情報からMemoryTableクラスを自動生成できるツールを検討（やりすぎ感）

        [MenuItem("Project/MasterMemory/MasterDataWindow")]
        public static void Open()
        {
            var w = GetWindow<MasterDataWindow>(nameof(MasterDataWindow));
            w.UpdateMemoryTables();
        }

        private void UpdateMemoryTables()
        {
            _memoryTables = MasterDataHelper.GetMemoryTableTypes();
            Repaint();
        }

        private Type[] _memoryTables = Array.Empty<Type>();
        private Type _memoryTable;
        private int _selectedIndex;
        private bool _replaceToggle = true;
        private bool _backupToggle;
        private Vector2 _tableScrollPosition = Vector2.zero;
        private Vector2 _logScrollPosition = Vector2.zero;
        private StringBuilder _logBuilder = new();
        private char _logSeparator = '\n';

        private void OnGUI()
        {
            GUILayout.Space(10);

            // ヘッダー

            GUILayout.Space(10);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label("MemoryTables");
                    using (new EditorGUI.DisabledScope(!_memoryTables.Any()))
                    {
                        using (var scroller = new EditorGUILayout.ScrollViewScope(_tableScrollPosition, "box"))
                        {
                            _tableScrollPosition = scroller.scrollPosition;
                            var tableNames = _memoryTables.Select(x => x.Name).ToArray();
                            foreach (var tableName in tableNames)
                            {
                                EditorGUILayout.SelectableLabel($"{tableName}");
                            }
                        }
                    }
                }

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(360)))
                {
                    GUILayout.Label("マスタデータ作成メニュー");
                    using (new EditorGUI.DisabledScope(!_memoryTables.Any()))
                    {
                        var options = new[] { "作成＆更新するマスタを選択してください" }
                            .Concat(_memoryTables.Select(x => x.Name))
                            .ToArray();
                        _selectedIndex = EditorGUILayout.Popup(_selectedIndex, options);

                        if (_selectedIndex > 0 && _memoryTables.Any())
                        {
                            _memoryTable = _memoryTables[_selectedIndex - 1];
                        }

                        _replaceToggle = EditorGUILayout.ToggleLeft("既に存在するTsvデータを引継ぎ、最新のテーブル定義情報で更新する", _replaceToggle);
                        _backupToggle = EditorGUILayout.ToggleLeft("更新前にバックアップを作成", _backupToggle);

                        using (new EditorGUI.DisabledScope(_selectedIndex <= 0))
                        {
                            // GUILayout.FlexibleSpace();

                            if (GUILayout.Button("Tsv作成＆更新"))
                            {
                                AppendLog($"Tsv作成: {_memoryTable.Name}");
                                MasterDataHelper.GenerateTsv(_memoryTable, _replaceToggle, _backupToggle);
                            }
                        }

                        if (GUILayout.Button("Tsv一括作成＆更新"))
                        {
                            AppendLog($"Tsv一括作成: {_memoryTables.Length}件");
                            MasterDataHelper.GenerateTsvAll(_replaceToggle, _replaceToggle);
                        }

                        if (GUILayout.Button("マスタデータバイナリ作成"))
                        {
                            AppendLog($"マスタデータバイナリ作成: {_memoryTables.Length}件");
                            MasterDataHelper.GenerateMasterDataBinary();
                        }
                    }

                    if (GUILayout.Button("マスタデータバイナリ読込テスト"))
                    {
                        AppendLog($"マスタデータバイナリ読込: {_memoryTables.Length}件");
                        MasterDataBinaryWindow.Open();
                    }

                    if (GUILayout.Button("最新状態を取得"))
                    {
                        UpdateMemoryTables();
                        AppendLog($"最新状態を取得 Tsv件数: {_memoryTables.Length}");
                    }

                    using (new EditorGUI.DisabledScope(_logBuilder.Length <= 0))
                    {
                        using (var scroller = new EditorGUILayout.ScrollViewScope(_logScrollPosition, "box"))
                        {
                            _logScrollPosition = scroller.scrollPosition;
                            var logs = _logBuilder.ToString().Split('\r', '\n');
                            foreach (var log in logs)
                            {
                                EditorGUILayout.LabelField($"{log}");
                            }
                        }
                    }
                }
            }

            GUILayout.Space(10);

            // ログ;

            GUILayout.Space(10);

            // フッター;
        }

        private void AppendLog(string log)
        {
            _logBuilder.Append(DateTime.Now + " " + log + _logSeparator);
        }
    }

    public class MasterDataBinaryWindow : EditorWindow
    {
        [MenuItem("Project/MasterMemory/MasterDataBinaryEditorWindow")]
        public static void Open()
        {
            var w = GetWindow<MasterDataBinaryWindow>(nameof(MasterDataBinaryWindow));
            w.UpdateMemoryDatabase();
        }

        private void UpdateMemoryDatabase()
        {
            _memoryDatabase = MasterDataHelper.LoadMasterDataBinary();
            Repaint();
        }

        private MemoryDatabase _memoryDatabase;
        private Vector2 _tableScrollPosition;

        private void OnGUI()
        {
            GUILayout.Space(10);

            // ヘッダー

            GUILayout.Space(10);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label("MemoryDatabase");
                    using (new EditorGUI.DisabledScope(_memoryDatabase is null))
                    {
                        using (var scroller = new EditorGUILayout.ScrollViewScope(_tableScrollPosition, "box"))
                        {
                            _tableScrollPosition = scroller.scrollPosition;
                            if (_memoryDatabase != null)
                            {
                                var tables = _memoryDatabase
                                    .GetType()
                                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                    .ToDictionary(x => x.PropertyType, x => x.GetValue(_memoryDatabase));

                                // GetRawDataUnsafe()

                                foreach (var (type, instance) in tables)
                                {
                                    var count = type.GetProperty("Count")?.GetValue(instance);
                                    EditorGUILayout.SelectableLabel($"テーブル名: {type.Name} データ件数: {count}");

                                    // var methodInfo = type.GetMethod("GetRawDataUnsafe");
                                    // var value = methodInfo?.Invoke(instance, null);
                                    // Type returnType = methodInfo?.ReturnType;
                                    // var ret = (object[])MasterDataHelper.ConvertToType(value, returnType);
                                    // foreach (var r in ret)
                                    // {
                                    //     EditorGUILayout.SelectableLabel($"{r}");
                                    // }
                                }
                            }
                        }
                    }

                    if (GUILayout.Button("マスタデータバイナリ読込テスト"))
                    {
                        UpdateMemoryDatabase();
                    }
                }

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(320)))
                {
                }
            }

            GUILayout.Space(10);

            // ログ;

            GUILayout.Space(10);

            // フッター;
        }
    }
}