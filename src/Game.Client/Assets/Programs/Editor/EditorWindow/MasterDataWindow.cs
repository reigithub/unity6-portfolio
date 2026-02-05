using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Game.Client.MasterData;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public class MasterDataWindow : EditorWindow
    {
        [MenuItem("Project/MasterMemory/MasterDataWindow")]
        public static void Open()
        {
            var w = GetWindow<MasterDataWindow>(nameof(MasterDataWindow));
            w.UpdateMemoryTables();
        }

        [MenuItem("Project/MasterMemory/GenerateMasterDataBinary")]
        public static void GenerateBinaryCli()
        {
            Debug.Log("[MasterDataWindow] Building MasterDataBinary via CLI...");
            var result = GameToolsRunner.BuildClient();
            if (result.Success)
            {
                Debug.Log($"[MasterDataWindow] MasterDataBinary generated successfully.\n{result.Output}");
                AssetDatabase.Refresh();
            }
            else
            {
                Debug.LogError($"[MasterDataWindow] Build failed.\n{result.GetCombinedOutput()}");
            }
        }

        private void UpdateMemoryTables()
        {
            _memoryTables = MasterDataHelper.GetMemoryTableTypes();
            Repaint();
        }

        private Type[] _memoryTables = Array.Empty<Type>();
        private bool _isProcessing;
        private Vector2 _tableScrollPosition = Vector2.zero;
        private Vector2 _logScrollPosition = Vector2.zero;
        private StringBuilder _logBuilder = new();

        private void OnGUI()
        {
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

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(400)))
                {
                    // === CLI Mode Section ===
                    EditorGUILayout.LabelField("Game.Tools CLI", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox("CLIコマンドを使用してマスターデータを管理します。\nProtoスキーマ検証、クライアント/サーバー両対応。", MessageType.Info);

                    using (new EditorGUI.DisabledScope(_isProcessing))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button("コード生成 (codegen)", GUILayout.Height(28)))
                            {
                                RunCliCommand("codegen", () => GameToolsRunner.Codegen());
                            }
                            if (GUILayout.Button("TSV検証 (validate)", GUILayout.Height(28)))
                            {
                                RunCliCommand("validate", () => GameToolsRunner.Validate());
                            }
                        }

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button("Client バイナリ生成", GUILayout.Height(28)))
                            {
                                RunCliCommand("build-client", () => GameToolsRunner.BuildClient());
                            }
                            if (GUILayout.Button("Server バイナリ生成", GUILayout.Height(28)))
                            {
                                RunCliCommand("build-server", () => GameToolsRunner.BuildServer());
                            }
                        }

                        if (GUILayout.Button("全て生成 (Client + Server)", GUILayout.Height(32)))
                        {
                            RunCliCommand("build-all", () => GameToolsRunner.BuildAll());
                        }
                    }

                    GUILayout.Space(10);
                    EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                    GUILayout.Space(5);

                    // === Common Section ===
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("バイナリ読込テスト"))
                        {
                            AppendLog($"マスタデータバイナリ読込: {_memoryTables.Length}件");
                            MasterDataBinaryWindow.Open();
                        }

                        if (GUILayout.Button("最新状態を取得"))
                        {
                            UpdateMemoryTables();
                            AppendLog($"最新状態を取得 テーブル件数: {_memoryTables.Length}");
                        }
                    }

                    GUILayout.Space(5);

                    // === Log Section ===
                    GUILayout.Label("ログ出力");
                    using (var scroller = new EditorGUILayout.ScrollViewScope(_logScrollPosition, "box", GUILayout.Height(150)))
                    {
                        _logScrollPosition = scroller.scrollPosition;
                        var logs = _logBuilder.ToString().Split('\r', '\n');
                        foreach (var log in logs)
                        {
                            if (!string.IsNullOrWhiteSpace(log))
                            {
                                EditorGUILayout.LabelField(log, EditorStyles.wordWrappedMiniLabel);
                            }
                        }
                    }

                    if (GUILayout.Button("ログクリア", GUILayout.Width(80)))
                    {
                        _logBuilder.Clear();
                    }
                }
            }

            GUILayout.Space(10);
        }

        private void RunCliCommand(string commandName, Func<GameToolsResult> command)
        {
            _isProcessing = true;
            AppendLog($"[CLI] {commandName} 実行中...");
            Repaint();

            EditorApplication.delayCall += () =>
            {
                try
                {
                    var result = command();
                    if (result.Success)
                    {
                        AppendLog($"[CLI] {commandName} 完了");
                        var output = result.Output;
                        if (output.Length > 500)
                        {
                            output = output.Substring(0, 500) + "\n... (省略)";
                        }
                        AppendLog(output);
                        AssetDatabase.Refresh();
                    }
                    else
                    {
                        AppendLog($"[CLI] {commandName} 失敗 (ExitCode: {result.ExitCode})");
                        AppendLog(result.GetCombinedOutput());
                        Debug.LogError($"[MasterDataWindow] {commandName} failed:\n{result.GetCombinedOutput()}");
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($"[CLI] {commandName} エラー: {ex.Message}");
                    Debug.LogError($"[MasterDataWindow] {commandName} error: {ex}");
                }
                finally
                {
                    _isProcessing = false;
                    Repaint();
                }
            };
        }

        private void AppendLog(string log)
        {
            var lines = log.Split('\n');
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    _logBuilder.AppendLine($"{DateTime.Now:HH:mm:ss} {line.Trim()}");
                }
            }
            _logScrollPosition = new Vector2(0, float.MaxValue);
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

                                foreach (var (type, instance) in tables)
                                {
                                    var count = type.GetProperty("Count")?.GetValue(instance);
                                    EditorGUILayout.SelectableLabel($"テーブル名: {type.Name} データ件数: {count}");
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
        }
    }
}
