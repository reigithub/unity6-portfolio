using System;
using System.Collections.Generic;
using System.Linq;
using Game.Shared.Scriptable.Database.Validation;
using UnityEditor;
using UnityEngine;

namespace Game.Shared.Scriptable.Database.EditorTools
{
    /// <summary>
    /// マスターデータ検証の結果をテーブルごとに一覧・詳細表示するウィンドウ。
    /// Console 出力だけの <see cref="ScriptableDatabaseValidationRunner"/> と違い、
    /// どのテーブルが未検証かを一覧で保持し、エラー箇所から資産へ辿れる。
    /// </summary>
    public class ScriptableDatabaseValidationWindow : EditorWindow
    {
        private sealed class Entry
        {
            public Type RecordType;
            public ScriptableTableBase Table;
            public bool IsSelected;
            public ValidationResult Result;
        }

        private readonly List<Entry> _entries = new();
        private ValidationResult _configurationResult;

        // 詳細ペインに出す対象。null は構成チェック結果を指す。
        private Type _selected;

        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private GUIStyle _errorStyle;

        [MenuItem("Project/Database/ScriptableDatabaseValidationWindow")]
        public static void Open() => GetWindow<ScriptableDatabaseValidationWindow>("Database Validation");

        private void OnEnable()
        {
            minSize = new Vector2(640, 320);
            Refresh();
        }

        private void OnGUI()
        {
            _errorStyle ??= new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = new Color(1f, 0.4f, 0.4f) },
                wordWrap = true,
            };

            DrawToolbar();

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawList();
                DrawDetails();
            }
        }

        // ---- 一覧 ----

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("更新", EditorStyles.toolbarButton, GUILayout.Width(60))) Refresh();
                if (GUILayout.Button("すべて検証", EditorStyles.toolbarButton, GUILayout.Width(80))) Validate(_entries);

                using (new EditorGUI.DisabledScope(!_entries.Any(e => e.IsSelected)))
                {
                    if (GUILayout.Button("選択項目を検証", EditorStyles.toolbarButton, GUILayout.Width(100)))
                        Validate(_entries.Where(e => e.IsSelected).ToList());
                }

                GUILayout.FlexibleSpace();

                int validated = _entries.Count(e => e.Result != null);
                int errors = _entries.Count(e => e.Result != null && e.Result.HasErrors)
                    + (_configurationResult != null && _configurationResult.HasErrors ? 1 : 0);
                EditorGUILayout.LabelField($"検証済み {validated}/{_entries.Count}｜エラー {errors} 件", EditorStyles.miniLabel);
            }
        }

        private void DrawList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(260)))
            {
                _listScroll = EditorGUILayout.BeginScrollView(_listScroll);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(24); // トグルの無い行なので位置を揃える
                    DrawStatus(_configurationResult);
                    if (GUILayout.Button(ValidationExecutor.ConfigurationResultName, EditorStyles.label)) _selected = null;
                }

                foreach (var entry in _entries)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        entry.IsSelected = EditorGUILayout.Toggle(entry.IsSelected, GUILayout.Width(20));
                        DrawStatus(entry.Result);
                        if (GUILayout.Button(entry.RecordType.Name, EditorStyles.label)) _selected = entry.RecordType;
                    }
                }

                EditorGUILayout.EndScrollView();

                if (_entries.Count == 0)
                    EditorGUILayout.HelpBox("検証対象のテーブルがありません。ScriptableDatabaseWindow の Build / Register を実行してください。", MessageType.Info);
            }
        }

        private void DrawStatus(ValidationResult result)
        {
            if (result == null)
            {
                EditorGUILayout.LabelField("--", GUILayout.Width(28));
                return;
            }

            if (!result.HasErrors)
            {
                EditorGUILayout.LabelField("OK", GUILayout.Width(28));
                return;
            }

            EditorGUILayout.LabelField("NG", _errorStyle, GUILayout.Width(28));
            EditorGUILayout.LabelField($"({result.Errors.Sum(x => x.Value.Count)})", _errorStyle, GUILayout.Width(40));
        }

        // ---- 詳細 ----

        private void DrawDetails()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                var entry = _entries.FirstOrDefault(e => e.RecordType == _selected);
                var result = _selected == null ? _configurationResult : entry?.Result;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(_selected?.Name ?? ValidationExecutor.ConfigurationResultName, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();

                    using (new EditorGUI.DisabledScope(entry == null))
                    {
                        if (GUILayout.Button("検証", GUILayout.Width(70))) Validate(new[] { entry });
                        if (GUILayout.Button("資産を選択", GUILayout.Width(90)))
                        {
                            Selection.activeObject = entry.Table;
                            EditorGUIUtility.PingObject(entry.Table);
                        }
                    }
                }

                if (result == null)
                {
                    EditorGUILayout.HelpBox("まだ検証していません。", MessageType.Info);
                    return;
                }

                EditorGUILayout.LabelField(ScriptableDatabaseValidationRunner.Header(result));

                if (!result.HasErrors)
                {
                    EditorGUILayout.HelpBox("エラーはありません。", MessageType.Info);
                    return;
                }

                _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
                foreach (var pair in result.Errors)
                {
                    EditorGUILayout.LabelField(pair.Key, EditorStyles.miniBoldLabel);
                    foreach (var message in pair.Value)
                    {
                        EditorGUILayout.LabelField($"    {message}", _errorStyle);
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        // ---- 実行 ----

        private void Refresh()
        {
            _entries.Clear();
            _configurationResult = null;
            _selected = null;

            var database = ScriptableDatabaseIO.LoadDatabaseOrNull();
            if (database == null) return;

            foreach (var (recordType, table) in ValidationExecutor.WiredTables(database))
            {
                _entries.Add(new Entry { RecordType = recordType, Table = table });
            }
        }

        private void Validate(IReadOnlyList<Entry> targets)
        {
            var database = ScriptableDatabaseIO.LoadDatabaseOrNull();
            if (database == null)
            {
                Debug.LogError("[Validation] 対象の ScriptableDatabase がありません。先に ScriptableDatabaseWindow の 'Build' / 'Register' を実行してください。");
                return;
            }

            ValidationExecutor executor;
            try
            {
                executor = ValidationExecutor.Create(database);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Validation] 検証を実行できません: {e}", database);
                return;
            }

            // 構成チェックは対象の選び方によらず常に最新へ更新する（テーブル検証の前提そのものを表すため）。
            _configurationResult = executor.ConfigurationResult;

            foreach (var entry in targets)
            {
                entry.Result = Execute(executor, entry.RecordType);
            }

            Repaint();
        }

        // 一覧が古く実行器の対象と食い違う場合も、その行の結果として見えるようにする。
        private static ValidationResult Execute(ValidationExecutor executor, Type recordType)
        {
            try
            {
                return executor.Execute(recordType);
            }
            catch (Exception e)
            {
                var result = new ValidationResult(recordType.Name, -1);
                result.AddError(recordType.Name, e.ToString());
                return result;
            }
        }
    }
}
