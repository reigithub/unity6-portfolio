using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// スクリーンショット撮影用エディタウィンドウ
    /// Game Viewのキャプチャを簡単に行えるツール
    /// </summary>
    public class ScreenshotCaptureWindow : EditorWindow
    {
        private enum ScreenshotPreset
        {
            Custom,
            MvcTitle,
            MvcGameplay,
            MvcResult,
            MvpTitle,
            MvpGameplay,
            MvpLevelup,
            MvpResult,
            ShaderToon,
            ShaderDissolve,
            EditorWindow
        }

        private static readonly string DefaultSavePath = "Documentation/Screenshots";

        private string _savePath;
        private string _fileName = "screenshot";
        private int _superSize = 1;
        private ScreenshotPreset _preset = ScreenshotPreset.Custom;
        private bool _openFolderAfterCapture = true;
        private Vector2 _scrollPosition;

        [MenuItem("Tools/Screenshot Capture Window")]
        public static void ShowWindow()
        {
            var window = GetWindow<ScreenshotCaptureWindow>("Screenshot Capture");
            window.minSize = new Vector2(350, 400);
        }

        private void OnEnable()
        {
            _savePath = Path.Combine(Application.dataPath, "..", DefaultSavePath);
            EnsureDirectoryExists(_savePath);
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Screenshot Capture Tool", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Game Viewのスクリーンショットを撮影します。\n" +
                "撮影前にGame Viewを表示し、目的のシーンを再生してください。",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // プリセット選択
            DrawPresetSection();

            EditorGUILayout.Space(10);

            // 保存設定
            DrawSaveSettings();

            EditorGUILayout.Space(10);

            // 撮影ボタン
            DrawCaptureButtons();

            EditorGUILayout.Space(10);

            // クイックキャプチャ（プリセット一覧）
            DrawQuickCaptureSection();

            EditorGUILayout.Space(10);

            // ユーティリティ
            DrawUtilitySection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawPresetSection()
        {
            EditorGUILayout.LabelField("プリセット", EditorStyles.boldLabel);

            var newPreset = (ScreenshotPreset)EditorGUILayout.EnumPopup("撮影対象", _preset);
            if (newPreset != _preset)
            {
                _preset = newPreset;
                _fileName = GetPresetFileName(_preset);
            }
        }

        private void DrawSaveSettings()
        {
            EditorGUILayout.LabelField("保存設定", EditorStyles.boldLabel);

            // 保存先フォルダ
            EditorGUILayout.BeginHorizontal();
            _savePath = EditorGUILayout.TextField("保存先", _savePath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var path = EditorUtility.OpenFolderPanel("保存先フォルダを選択", _savePath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    _savePath = path;
                }
            }
            EditorGUILayout.EndHorizontal();

            // ファイル名
            _fileName = EditorGUILayout.TextField("ファイル名", _fileName);

            // 解像度倍率
            _superSize = EditorGUILayout.IntSlider("解像度倍率", _superSize, 1, 4);
            EditorGUILayout.HelpBox(
                $"現在の設定: {Screen.width * _superSize} x {Screen.height * _superSize} px",
                MessageType.None);

            // オプション
            _openFolderAfterCapture = EditorGUILayout.Toggle("撮影後にフォルダを開く", _openFolderAfterCapture);
        }

        private void DrawCaptureButtons()
        {
            EditorGUILayout.LabelField("撮影", EditorStyles.boldLabel);

            // メイン撮影ボタン
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button($"スクリーンショットを撮影\n({_fileName}.png)", GUILayout.Height(50)))
            {
                CaptureScreenshot(_fileName);
            }
            GUI.backgroundColor = Color.white;

            // タイムスタンプ付き撮影
            if (GUILayout.Button("タイムスタンプ付きで撮影"))
            {
                var timestampName = $"{_fileName}_{DateTime.Now:yyyyMMdd_HHmmss}";
                CaptureScreenshot(timestampName);
            }
        }

        private void DrawQuickCaptureSection()
        {
            EditorGUILayout.LabelField("クイックキャプチャ", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("プリセット名でワンクリック撮影", MessageType.None);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // MVC
            EditorGUILayout.LabelField("MVC: ScoreTimeAttack", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Title")) CaptureScreenshot("mvc_title");
            if (GUILayout.Button("Gameplay")) CaptureScreenshot("mvc_gameplay");
            if (GUILayout.Button("Result")) CaptureScreenshot("mvc_result");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // MVP
            EditorGUILayout.LabelField("MVP: Survivor", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Title")) CaptureScreenshot("mvp_title");
            if (GUILayout.Button("Gameplay")) CaptureScreenshot("mvp_gameplay");
            if (GUILayout.Button("LevelUp")) CaptureScreenshot("mvp_levelup");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Result")) CaptureScreenshot("mvp_result");
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Shader/Effects
            EditorGUILayout.LabelField("Shader/Effects", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Toon")) CaptureScreenshot("shader_toon");
            if (GUILayout.Button("Dissolve")) CaptureScreenshot("shader_dissolve");
            if (GUILayout.Button("Editor")) CaptureScreenshot("editor_window");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawUtilitySection()
        {
            EditorGUILayout.LabelField("ユーティリティ", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("保存先フォルダを開く"))
            {
                OpenFolder(_savePath);
            }
            if (GUILayout.Button("デフォルトパスにリセット"))
            {
                _savePath = Path.Combine(Application.dataPath, "..", DefaultSavePath);
                EnsureDirectoryExists(_savePath);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 撮影チェックリスト
            EditorGUILayout.LabelField("撮影チェックリスト", EditorStyles.miniLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawChecklistItem("mvc_title.png");
            DrawChecklistItem("mvc_gameplay.png");
            DrawChecklistItem("mvc_result.png");
            DrawChecklistItem("mvp_title.png");
            DrawChecklistItem("mvp_gameplay.png");
            DrawChecklistItem("mvp_levelup.png");
            DrawChecklistItem("mvp_result.png");
            DrawChecklistItem("shader_toon.png");
            DrawChecklistItem("shader_dissolve.png");
            DrawChecklistItem("editor_window.png");
            EditorGUILayout.EndVertical();
        }

        private void DrawChecklistItem(string fileName)
        {
            var filePath = Path.Combine(_savePath, fileName);
            var exists = File.Exists(filePath);
            var icon = exists ? "\u2714" : "\u2610"; // チェックマーク or 空チェックボックス
            var style = exists ? EditorStyles.miniLabel : EditorStyles.miniLabel;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{icon} {fileName}", style);
            if (exists)
            {
                if (GUILayout.Button("View", GUILayout.Width(50)))
                {
                    EditorUtility.RevealInFinder(filePath);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void CaptureScreenshot(string fileName)
        {
            EnsureDirectoryExists(_savePath);

            var fullPath = Path.Combine(_savePath, $"{fileName}.png");

            // 既存ファイルの確認
            if (File.Exists(fullPath))
            {
                if (!EditorUtility.DisplayDialog(
                    "上書き確認",
                    $"{fileName}.png は既に存在します。上書きしますか？",
                    "上書き", "キャンセル"))
                {
                    return;
                }
            }

            // スクリーンショット撮影
            ScreenCapture.CaptureScreenshot(fullPath, _superSize);

            Debug.Log($"[Screenshot] Captured: {fullPath}");
            ShowNotification(new GUIContent($"Captured: {fileName}.png"));

            // 撮影後にフォルダを開く
            if (_openFolderAfterCapture)
            {
                // 少し遅延させてファイル書き込み完了を待つ
                EditorApplication.delayCall += () =>
                {
                    EditorApplication.delayCall += () =>
                    {
                        if (File.Exists(fullPath))
                        {
                            EditorUtility.RevealInFinder(fullPath);
                        }
                    };
                };
            }

            // ウィンドウを再描画してチェックリストを更新
            Repaint();
        }

        private static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                Debug.Log($"[Screenshot] Created directory: {path}");
            }
        }

        private static void OpenFolder(string path)
        {
            if (Directory.Exists(path))
            {
                EditorUtility.RevealInFinder(path);
            }
            else
            {
                EditorUtility.DisplayDialog("エラー", $"フォルダが存在しません:\n{path}", "OK");
            }
        }

        private static string GetPresetFileName(ScreenshotPreset preset)
        {
            return preset switch
            {
                ScreenshotPreset.MvcTitle => "mvc_title",
                ScreenshotPreset.MvcGameplay => "mvc_gameplay",
                ScreenshotPreset.MvcResult => "mvc_result",
                ScreenshotPreset.MvpTitle => "mvp_title",
                ScreenshotPreset.MvpGameplay => "mvp_gameplay",
                ScreenshotPreset.MvpLevelup => "mvp_levelup",
                ScreenshotPreset.MvpResult => "mvp_result",
                ScreenshotPreset.ShaderToon => "shader_toon",
                ScreenshotPreset.ShaderDissolve => "shader_dissolve",
                ScreenshotPreset.EditorWindow => "editor_window",
                _ => "screenshot"
            };
        }
    }
}
