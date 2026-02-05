using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public class DatabaseWindow : EditorWindow
    {
        [MenuItem("Project/Database/DatabaseWindow")]
        public static void Open()
        {
            GetWindow<DatabaseWindow>(nameof(DatabaseWindow));
        }

        private bool _isProcessing;
        private Vector2 _logScrollPosition = Vector2.zero;
        private StringBuilder _logBuilder = new();
        private string _selectedSchema = "";
        private int _rollbackSteps = 1;
        private bool _seedAfterReset;

        private readonly string[] _schemaOptions = { "All", "master", "user" };
        private int _selectedSchemaIndex;

        private void OnGUI()
        {
            GUILayout.Space(10);

            using (new EditorGUILayout.VerticalScope())
            {
                // === Migration Section ===
                EditorGUILayout.LabelField("Database Migration", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("PostgreSQLデータベースのマイグレーションを管理します。\n接続設定: src/Game.Server/appsettings.json", MessageType.Info);

                GUILayout.Space(5);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Schema:", GUILayout.Width(60));
                    _selectedSchemaIndex = EditorGUILayout.Popup(_selectedSchemaIndex, _schemaOptions, GUILayout.Width(100));
                    _selectedSchema = _selectedSchemaIndex == 0 ? "" : _schemaOptions[_selectedSchemaIndex];
                }

                GUILayout.Space(5);

                using (new EditorGUI.DisabledScope(_isProcessing))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Migrate Up", GUILayout.Height(28)))
                        {
                            RunCliCommand("migrate up", () => GameToolsRunner.MigrateUp(_selectedSchema));
                        }
                        if (GUILayout.Button("Status", GUILayout.Height(28)))
                        {
                            RunCliCommand("migrate status", () => GameToolsRunner.MigrateStatus(_selectedSchema));
                        }
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Steps:", GUILayout.Width(45));
                        _rollbackSteps = EditorGUILayout.IntField(_rollbackSteps, GUILayout.Width(40));
                        if (_rollbackSteps < 1) _rollbackSteps = 1;

                        if (GUILayout.Button("Migrate Down (Rollback)", GUILayout.Height(22)))
                        {
                            RunCliCommand("migrate down", () => GameToolsRunner.MigrateDown(_rollbackSteps, _selectedSchema));
                        }
                    }

                    GUILayout.Space(5);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _seedAfterReset = EditorGUILayout.ToggleLeft("Seed after reset", _seedAfterReset, GUILayout.Width(120));
                        if (GUILayout.Button("Reset Database", GUILayout.Height(22)))
                        {
                            if (EditorUtility.DisplayDialog(
                                    "Database Reset",
                                    "WARNING: This will DROP all tables and re-create them.\n\nAre you sure?",
                                    "Yes, Reset",
                                    "Cancel"))
                            {
                                RunCliCommand("migrate reset", () => GameToolsRunner.MigrateReset(_seedAfterReset, _selectedSchema));
                            }
                        }
                    }
                }

                GUILayout.Space(10);
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                GUILayout.Space(5);

                // === SeedData Section ===
                EditorGUILayout.LabelField("Seed Data", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("TSVファイルとデータベース間でデータを同期します。", MessageType.Info);

                GUILayout.Space(5);

                using (new EditorGUI.DisabledScope(_isProcessing))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Seed (TSV → DB)", GUILayout.Height(28)))
                        {
                            RunCliCommand("seeddata seed", () => GameToolsRunner.SeedData());
                        }
                        if (GUILayout.Button("Dump (DB → TSV)", GUILayout.Height(28)))
                        {
                            RunCliCommand("seeddata dump", () => GameToolsRunner.DumpData());
                        }
                    }

                    if (GUILayout.Button("Diff (Compare TSVs)", GUILayout.Height(24)))
                    {
                        RunCliCommand("seeddata diff", () => GameToolsRunner.DiffData("masterdata/raw/", "masterdata/dump/"));
                    }
                }

                GUILayout.Space(10);
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                GUILayout.Space(5);

                // === Log Section ===
                GUILayout.Label("Log Output");
                using (var scroller = new EditorGUILayout.ScrollViewScope(_logScrollPosition, "box", GUILayout.Height(200)))
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

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Clear Log", GUILayout.Width(80)))
                    {
                        _logBuilder.Clear();
                    }
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Open scripts/migrate/", GUILayout.Width(130)))
                    {
                        OpenFolder("scripts/migrate");
                    }
                    if (GUILayout.Button("Open scripts/seeddata/", GUILayout.Width(130)))
                    {
                        OpenFolder("scripts/seeddata");
                    }
                }
            }

            GUILayout.Space(10);
        }

        private void RunCliCommand(string commandName, Func<GameToolsResult> command)
        {
            _isProcessing = true;
            AppendLog($"[CLI] {commandName} executing...");
            Repaint();

            EditorApplication.delayCall += () =>
            {
                try
                {
                    var result = command();
                    if (result.Success)
                    {
                        AppendLog($"[CLI] {commandName} completed");
                        var output = result.Output;
                        if (output.Length > 1000)
                        {
                            output = output.Substring(0, 1000) + "\n... (truncated)";
                        }
                        AppendLog(output);
                    }
                    else
                    {
                        AppendLog($"[CLI] {commandName} failed (ExitCode: {result.ExitCode})");
                        AppendLog(result.GetCombinedOutput());
                        Debug.LogError($"[DatabaseWindow] {commandName} failed:\n{result.GetCombinedOutput()}");
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($"[CLI] {commandName} error: {ex.Message}");
                    Debug.LogError($"[DatabaseWindow] {commandName} error: {ex}");
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

        private static void OpenFolder(string relativePath)
        {
            var fullPath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(Application.dataPath, "..", "..", "..", relativePath));
            if (System.IO.Directory.Exists(fullPath))
            {
                EditorUtility.RevealInFinder(fullPath);
            }
            else
            {
                Debug.LogWarning($"Folder not found: {fullPath}");
            }
        }
    }
}
