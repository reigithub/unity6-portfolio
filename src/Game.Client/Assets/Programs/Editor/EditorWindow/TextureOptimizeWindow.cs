using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// テクスチャ一括最適化ウィンドウ
    /// 指定フォルダ内のテクスチャを一括で最適化設定に変更
    /// </summary>
    public class TextureOptimizeWindow : EditorWindow
    {
        [MenuItem("Tools/Texture Optimize Window")]
        public static void ShowWindow()
        {
            var window = GetWindow<TextureOptimizeWindow>("Texture Optimizer");
            window.minSize = new Vector2(500, 600);
        }

        // 設定
        private string _targetFolder = "Assets/StoreAssets/POLYART_Ancient Village";
        private int _maxTextureSize = 1024;
        private bool _enableCrunchCompression = true;
        private int _compressionQuality = 75;
        private bool _enableMipmapStreaming = true;
        private bool _includeSubfolders = true;

        // フィルター
        private bool _optimizeBaseColor = true;
        private bool _optimizeNormal = true;
        private bool _optimizeAO = true;
        private bool _optimizeOther = true;

        // プレビュー
        private List<TextureInfo> _previewTextures = new();
        private Vector2 _scrollPosition;
        private long _estimatedSavings;
        private bool _showPreview;

        // 進捗
        private bool _isProcessing;
        private int _processedCount;
        private int _totalCount;

        private class TextureInfo
        {
            public string Path;
            public string Name;
            public TextureImporter Importer;
            public int CurrentMaxSize;
            public bool CurrentCrunched;
            public long FileSize;
            public TextureType Type;
            public bool Selected;
        }

        private enum TextureType
        {
            BaseColor,
            Normal,
            AO,
            Other
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Texture Batch Optimizer", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            using (new EditorGUI.DisabledScope(_isProcessing))
            {
                DrawFolderSelection();
                EditorGUILayout.Space(10);

                DrawOptimizationSettings();
                EditorGUILayout.Space(10);

                DrawTextureFilters();
                EditorGUILayout.Space(10);

                DrawActionButtons();
            }

            if (_showPreview)
            {
                EditorGUILayout.Space(10);
                DrawPreviewList();
            }

            if (_isProcessing)
            {
                DrawProgressBar();
            }
        }

        private void DrawFolderSelection()
        {
            EditorGUILayout.LabelField("Target Folder", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                _targetFolder = EditorGUILayout.TextField(_targetFolder);
                if (GUILayout.Button("Browse", GUILayout.Width(60)))
                {
                    var selected = EditorUtility.OpenFolderPanel("Select Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(selected))
                    {
                        // プロジェクトパスからの相対パスに変換
                        var projectPath = Application.dataPath.Replace("/Assets", "");
                        if (selected.StartsWith(projectPath))
                        {
                            _targetFolder = "Assets" + selected.Substring(Application.dataPath.Length);
                        }
                    }
                }
            }

            _includeSubfolders = EditorGUILayout.Toggle("Include Subfolders", _includeSubfolders);
        }

        private void DrawOptimizationSettings()
        {
            EditorGUILayout.LabelField("Optimization Settings", EditorStyles.boldLabel);

            _maxTextureSize = EditorGUILayout.IntPopup("Max Texture Size",
                _maxTextureSize,
                new[] { "256", "512", "1024", "2048", "4096" },
                new[] { 256, 512, 1024, 2048, 4096 });

            _enableCrunchCompression = EditorGUILayout.Toggle("Enable Crunch Compression", _enableCrunchCompression);

            using (new EditorGUI.DisabledScope(!_enableCrunchCompression))
            {
                _compressionQuality = EditorGUILayout.IntSlider("Compression Quality", _compressionQuality, 0, 100);
            }

            _enableMipmapStreaming = EditorGUILayout.Toggle("Enable Mipmap Streaming", _enableMipmapStreaming);

            // 推定削減量を表示
            EditorGUILayout.Space(5);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Estimated Size Reduction:", EditorStyles.miniLabel);
                if (_enableCrunchCompression)
                {
                    EditorGUILayout.LabelField("50-70% (Crunch enabled)", EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField("20-40% (Size reduction only)", EditorStyles.miniLabel);
                }
            }
        }

        private void DrawTextureFilters()
        {
            EditorGUILayout.LabelField("Texture Type Filters", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                _optimizeBaseColor = EditorGUILayout.ToggleLeft("BaseColor/Albedo", _optimizeBaseColor, GUILayout.Width(120));
                _optimizeNormal = EditorGUILayout.ToggleLeft("Normal Maps", _optimizeNormal, GUILayout.Width(100));
                _optimizeAO = EditorGUILayout.ToggleLeft("AO Maps", _optimizeAO, GUILayout.Width(80));
                _optimizeOther = EditorGUILayout.ToggleLeft("Other", _optimizeOther, GUILayout.Width(60));
            }
        }

        private void DrawActionButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan Textures", GUILayout.Height(30)))
                {
                    ScanTextures();
                }

                using (new EditorGUI.DisabledScope(_previewTextures.Count == 0))
                {
                    if (GUILayout.Button("Select All", GUILayout.Height(30), GUILayout.Width(80)))
                    {
                        foreach (var tex in _previewTextures)
                            tex.Selected = true;
                    }

                    if (GUILayout.Button("Deselect All", GUILayout.Height(30), GUILayout.Width(80)))
                    {
                        foreach (var tex in _previewTextures)
                            tex.Selected = false;
                    }
                }
            }

            EditorGUILayout.Space(5);

            using (new EditorGUI.DisabledScope(_previewTextures.Count == 0 || !_previewTextures.Any(t => t.Selected)))
            {
                var selectedCount = _previewTextures.Count(t => t.Selected);
                var buttonText = $"Apply Optimization ({selectedCount} textures)";

                GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
                if (GUILayout.Button(buttonText, GUILayout.Height(35)))
                {
                    if (EditorUtility.DisplayDialog("Confirm Optimization",
                            $"This will modify {selectedCount} texture import settings.\n\n" +
                            "Settings:\n" +
                            $"- Max Size: {_maxTextureSize}\n" +
                            $"- Crunch Compression: {(_enableCrunchCompression ? "Enabled" : "Disabled")}\n" +
                            $"- Compression Quality: {_compressionQuality}\n" +
                            $"- Mipmap Streaming: {(_enableMipmapStreaming ? "Enabled" : "Disabled")}\n\n" +
                            "Continue?",
                            "Apply", "Cancel"))
                    {
                        ApplyOptimization();
                    }
                }
                GUI.backgroundColor = Color.white;
            }
        }

        private void DrawPreviewList()
        {
            EditorGUILayout.LabelField($"Found Textures ({_previewTextures.Count})", EditorStyles.boldLabel);

            if (_estimatedSavings > 0)
            {
                var savingsText = FormatFileSize(_estimatedSavings);
                EditorGUILayout.HelpBox($"Estimated savings: ~{savingsText} (based on current selection)", MessageType.Info);
            }

            // ヘッダー
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUILayout.LabelField("", GUILayout.Width(20));
                EditorGUILayout.LabelField("Name", GUILayout.Width(250));
                EditorGUILayout.LabelField("Type", GUILayout.Width(80));
                EditorGUILayout.LabelField("Size", GUILayout.Width(70));
                EditorGUILayout.LabelField("Current Max", GUILayout.Width(80));
                EditorGUILayout.LabelField("Crunched", GUILayout.Width(70));
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(250));

            foreach (var tex in _previewTextures)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    tex.Selected = EditorGUILayout.Toggle(tex.Selected, GUILayout.Width(20));
                    EditorGUILayout.LabelField(tex.Name, GUILayout.Width(250));
                    EditorGUILayout.LabelField(tex.Type.ToString(), GUILayout.Width(80));
                    EditorGUILayout.LabelField(FormatFileSize(tex.FileSize), GUILayout.Width(70));
                    EditorGUILayout.LabelField(tex.CurrentMaxSize.ToString(), GUILayout.Width(80));
                    EditorGUILayout.LabelField(tex.CurrentCrunched ? "Yes" : "No", GUILayout.Width(70));
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawProgressBar()
        {
            EditorGUILayout.Space(10);
            var progress = _totalCount > 0 ? (float)_processedCount / _totalCount : 0;
            var rect = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.ProgressBar(rect, progress, $"Processing... {_processedCount}/{_totalCount}");
        }

        private void ScanTextures()
        {
            _previewTextures.Clear();
            _showPreview = true;

            if (!AssetDatabase.IsValidFolder(_targetFolder))
            {
                EditorUtility.DisplayDialog("Error", $"Folder not found: {_targetFolder}", "OK");
                return;
            }

            var searchOption = _includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var extensions = new[] { "*.png", "*.jpg", "*.jpeg", "*.tga", "*.psd", "*.tif", "*.tiff" };

            // プロジェクトルートからの絶対パスを構築
            // Application.dataPath = "C:/Project/Assets" なので、"Assets"部分を_targetFolderで置き換える
            var dataPath = Application.dataPath.Replace("\\", "/");
            string fullPath;

            if (_targetFolder.StartsWith("Assets"))
            {
                // "Assets/..." 形式の場合、"Assets"をApplication.dataPathで置換
                fullPath = dataPath + _targetFolder.Substring(6); // "Assets"の6文字をスキップ
            }
            else
            {
                fullPath = Path.Combine(dataPath, _targetFolder);
            }

            Debug.Log($"[TextureOptimizer] Application.dataPath: {dataPath}");
            Debug.Log($"[TextureOptimizer] Target folder: {_targetFolder}");
            Debug.Log($"[TextureOptimizer] Full path: {fullPath}");

            if (!Directory.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("Error", $"Directory not found: {fullPath}", "OK");
                return;
            }

            var firstFileLogged = false;

            foreach (var ext in extensions)
            {
                string[] files;
                try
                {
                    files = Directory.GetFiles(fullPath, ext, searchOption);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[TextureOptimizer] Error scanning for {ext}: {e.Message}");
                    continue;
                }

                Debug.Log($"[TextureOptimizer] Found {files.Length} {ext} files");

                foreach (var file in files)
                {
                    // ファイルパスをUnityアセットパスに変換
                    var normalizedFile = file.Replace("\\", "/");

                    // dataPathの親ディレクトリ（プロジェクトルート）を取得して、そこからの相対パスを作成
                    // Application.dataPath = ".../Assets" なので、Assetsの親がプロジェクトルート
                    string assetPath;
                    if (normalizedFile.StartsWith(dataPath))
                    {
                        // dataPath = ".../Assets", file = ".../Assets/xxx/yyy.png"
                        // assetPath = "Assets/xxx/yyy.png"
                        assetPath = "Assets" + normalizedFile.Substring(dataPath.Length);
                    }
                    else
                    {
                        if (!firstFileLogged)
                        {
                            Debug.LogWarning($"[TextureOptimizer] Path mismatch - File: {normalizedFile}, DataPath: {dataPath}");
                            firstFileLogged = true;
                        }
                        continue;
                    }

                    if (!firstFileLogged)
                    {
                        Debug.Log($"[TextureOptimizer] First file - Raw: {file}, AssetPath: {assetPath}");
                        firstFileLogged = true;
                    }

                    var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

                    if (importer == null)
                    {
                        Debug.LogWarning($"[TextureOptimizer] Failed to get importer for: {assetPath}");
                        continue;
                    }

                    var texType = DetermineTextureType(Path.GetFileName(file));

                    // フィルターチェック
                    if (!ShouldIncludeTexture(texType))
                        continue;

                    var fileInfo = new FileInfo(file);
                    var defaultSettings = importer.GetDefaultPlatformTextureSettings();

                    _previewTextures.Add(new TextureInfo
                    {
                        Path = assetPath,
                        Name = Path.GetFileName(file),
                        Importer = importer,
                        CurrentMaxSize = defaultSettings.maxTextureSize,
                        CurrentCrunched = defaultSettings.crunchedCompression,
                        FileSize = fileInfo.Length,
                        Type = texType,
                        Selected = true
                    });
                }
            }

            // サイズ順にソート
            _previewTextures = _previewTextures.OrderByDescending(t => t.FileSize).ToList();
            CalculateEstimatedSavings();

            Debug.Log($"[TextureOptimizer] Found {_previewTextures.Count} textures in {_targetFolder}");
        }

        private TextureType DetermineTextureType(string fileName)
        {
            var lower = fileName.ToLower();

            if (lower.Contains("basecolor") || lower.Contains("albedo") || lower.Contains("diffuse") || lower.Contains("_d."))
                return TextureType.BaseColor;

            if (lower.Contains("normal") || lower.Contains("_n.") || lower.Contains("_norm"))
                return TextureType.Normal;

            if (lower.Contains("_ao") || lower.Contains("occlusion") || lower.Contains("ambient"))
                return TextureType.AO;

            return TextureType.Other;
        }

        private bool ShouldIncludeTexture(TextureType type)
        {
            return type switch
            {
                TextureType.BaseColor => _optimizeBaseColor,
                TextureType.Normal => _optimizeNormal,
                TextureType.AO => _optimizeAO,
                TextureType.Other => _optimizeOther,
                _ => true
            };
        }

        private void CalculateEstimatedSavings()
        {
            long totalCurrentSize = 0;
            long estimatedNewSize = 0;

            foreach (var tex in _previewTextures.Where(t => t.Selected))
            {
                totalCurrentSize += tex.FileSize;

                // 推定圧縮率
                float compressionRatio = 1f;

                // サイズ削減による圧縮
                if (tex.CurrentMaxSize > _maxTextureSize)
                {
                    float sizeRatio = (float)_maxTextureSize / tex.CurrentMaxSize;
                    compressionRatio *= sizeRatio * sizeRatio; // 2D なので2乗
                }

                // Crunch圧縮による追加削減
                if (_enableCrunchCompression && !tex.CurrentCrunched)
                {
                    compressionRatio *= 0.3f; // Crunchで約70%削減
                }

                estimatedNewSize += (long)(tex.FileSize * compressionRatio);
            }

            _estimatedSavings = totalCurrentSize - estimatedNewSize;
        }

        private void ApplyOptimization()
        {
            var selectedTextures = _previewTextures.Where(t => t.Selected).ToList();
            _totalCount = selectedTextures.Count;
            _processedCount = 0;
            _isProcessing = true;

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var tex in selectedTextures)
                {
                    ApplySettingsToTexture(tex);
                    _processedCount++;

                    if (_processedCount % 10 == 0)
                    {
                        EditorUtility.DisplayProgressBar("Optimizing Textures",
                            $"Processing {tex.Name}...",
                            (float)_processedCount / _totalCount);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                _isProcessing = false;
            }

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Complete",
                $"Successfully optimized {_processedCount} textures.\n\n" +
                "Please rebuild your project to see the size reduction.",
                "OK");

            // リスト更新
            ScanTextures();
        }

        private void ApplySettingsToTexture(TextureInfo tex)
        {
            var importer = tex.Importer;

            // Mipmap Streaming
            importer.streamingMipmaps = _enableMipmapStreaming;

            // デフォルトプラットフォーム設定
            var defaultSettings = importer.GetDefaultPlatformTextureSettings();
            defaultSettings.maxTextureSize = _maxTextureSize;
            defaultSettings.crunchedCompression = _enableCrunchCompression;
            defaultSettings.compressionQuality = _compressionQuality;
            importer.SetPlatformTextureSettings(defaultSettings);

            // 各プラットフォーム設定もオーバーライド
            ApplyPlatformSettings(importer, "Standalone");
            ApplyPlatformSettings(importer, "Android");
            ApplyPlatformSettings(importer, "iPhone");
            ApplyPlatformSettings(importer, "WebGL");

            importer.SaveAndReimport();
        }

        private void ApplyPlatformSettings(TextureImporter importer, string platform)
        {
            var settings = importer.GetPlatformTextureSettings(platform);
            if (settings.overridden)
            {
                settings.maxTextureSize = Mathf.Min(settings.maxTextureSize, _maxTextureSize);
                settings.crunchedCompression = _enableCrunchCompression;
                settings.compressionQuality = _compressionQuality;
                importer.SetPlatformTextureSettings(settings);
            }
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes >= 1024 * 1024)
                return $"{bytes / (1024f * 1024f):F1} MB";
            if (bytes >= 1024)
                return $"{bytes / 1024f:F1} KB";
            return $"{bytes} B";
        }
    }
}
