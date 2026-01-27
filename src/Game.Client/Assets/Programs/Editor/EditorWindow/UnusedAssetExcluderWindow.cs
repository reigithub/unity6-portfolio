using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// 未使用アセットをビルドから除外するウィンドウ
    /// 特定のフォルダ内のアセットがシーンで使用されているかチェックし、
    /// 未使用の場合はAddressablesから除外またはインポート設定を変更
    /// </summary>
    public class UnusedAssetExcluderWindow : EditorWindow
    {
        [MenuItem("Tools/Unused Asset Excluder")]
        public static void ShowWindow()
        {
            var window = GetWindow<UnusedAssetExcluderWindow>("Unused Asset Excluder");
            window.minSize = new Vector2(600, 500);
        }

        // 設定
        private string _sceneToCheck = "Assets/ProjectAssets/UnityScenes/AncientVillageDay/AncientVillageDay.unity";
        private string _assetFolderToCheck = "Assets/StoreAssets/POLYART_Ancient Village";

        // プリセット除外パターン
        private bool _excludeBridge = true;
        private bool _excludeBoat = false;
        private bool _excludePortal = false;
        private string _customExcludePattern = "";

        // 結果
        private List<AssetInfo> _unusedAssets = new();
        private List<AssetInfo> _usedAssets = new();
        private Vector2 _scrollPosition;
        private bool _showResults;
        private long _totalUnusedSize;

        private class AssetInfo
        {
            public string Path;
            public string Name;
            public long FileSize;
            public string AssetType;
            public bool Selected;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Unused Asset Excluder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "指定シーンで使用されていないアセットを検出し、ビルドから除外します。\n" +
                "Bridgeなど大容量だが未使用のアセットを特定できます。",
                MessageType.Info);

            EditorGUILayout.Space(10);
            DrawSettings();

            EditorGUILayout.Space(10);
            DrawExcludePatterns();

            EditorGUILayout.Space(10);
            DrawActionButtons();

            if (_showResults)
            {
                EditorGUILayout.Space(10);
                DrawResults();
            }
        }

        private void DrawSettings()
        {
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Scene to Check", GUILayout.Width(100));
                _sceneToCheck = EditorGUILayout.TextField(_sceneToCheck);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    var selected = EditorUtility.OpenFilePanel("Select Scene", "Assets", "unity");
                    if (!string.IsNullOrEmpty(selected))
                    {
                        var dataPath = Application.dataPath.Replace("\\", "/");
                        if (selected.StartsWith(dataPath))
                        {
                            _sceneToCheck = "Assets" + selected.Substring(dataPath.Length);
                        }
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Asset Folder", GUILayout.Width(100));
                _assetFolderToCheck = EditorGUILayout.TextField(_assetFolderToCheck);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    var selected = EditorUtility.OpenFolderPanel("Select Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(selected))
                    {
                        var dataPath = Application.dataPath.Replace("\\", "/");
                        if (selected.StartsWith(dataPath))
                        {
                            _assetFolderToCheck = "Assets" + selected.Substring(dataPath.Length);
                        }
                    }
                }
            }
        }

        private void DrawExcludePatterns()
        {
            EditorGUILayout.LabelField("Exclude Patterns (検索対象)", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _excludeBridge = EditorGUILayout.ToggleLeft(
                    "Bridge (橋) - 約35MB",
                    _excludeBridge);

                _excludeBoat = EditorGUILayout.ToggleLeft(
                    "Boat (船)",
                    _excludeBoat);

                _excludePortal = EditorGUILayout.ToggleLeft(
                    "Portal (ポータル)",
                    _excludePortal);

                EditorGUILayout.Space(5);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Custom Pattern:", GUILayout.Width(100));
                    _customExcludePattern = EditorGUILayout.TextField(_customExcludePattern);
                }
                EditorGUILayout.LabelField("(カンマ区切りで複数指定可: Bridge,Boat,Portal)", EditorStyles.miniLabel);
            }
        }

        private void DrawActionButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan Unused Assets", GUILayout.Height(30)))
                {
                    ScanUnusedAssets();
                }

                using (new EditorGUI.DisabledScope(_unusedAssets.Count == 0))
                {
                    if (GUILayout.Button("Select All", GUILayout.Height(30), GUILayout.Width(80)))
                    {
                        foreach (var asset in _unusedAssets)
                            asset.Selected = true;
                    }

                    if (GUILayout.Button("Deselect All", GUILayout.Height(30), GUILayout.Width(80)))
                    {
                        foreach (var asset in _unusedAssets)
                            asset.Selected = false;
                    }
                }
            }

            EditorGUILayout.Space(5);

            using (new EditorGUI.DisabledScope(_unusedAssets.Count == 0 || !_unusedAssets.Any(a => a.Selected)))
            {
                var selectedCount = _unusedAssets.Count(a => a.Selected);
                var selectedSize = _unusedAssets.Where(a => a.Selected).Sum(a => a.FileSize);

                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button($"Exclude Selected Assets ({selectedCount} files, {FormatFileSize(selectedSize)})", GUILayout.Height(35)))
                {
                    if (EditorUtility.DisplayDialog("Confirm Exclusion",
                            $"以下のアセットをビルドから除外します:\n\n" +
                            $"- ファイル数: {selectedCount}\n" +
                            $"- 合計サイズ: {FormatFileSize(selectedSize)}\n\n" +
                            "除外方法:\n" +
                            "1. テクスチャ/メッシュのインポート設定を変更\n" +
                            "2. Addressablesから除外\n\n" +
                            "続行しますか？",
                            "Exclude", "Cancel"))
                    {
                        ExcludeSelectedAssets();
                    }
                }
                GUI.backgroundColor = Color.white;
            }
        }

        private void DrawResults()
        {
            EditorGUILayout.LabelField($"Unused Assets ({_unusedAssets.Count} files, {FormatFileSize(_totalUnusedSize)})", EditorStyles.boldLabel);

            if (_unusedAssets.Count == 0)
            {
                EditorGUILayout.HelpBox("指定パターンに一致する未使用アセットは見つかりませんでした。", MessageType.Info);
                return;
            }

            // ヘッダー
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUILayout.LabelField("", GUILayout.Width(20));
                EditorGUILayout.LabelField("Name", GUILayout.Width(250));
                EditorGUILayout.LabelField("Type", GUILayout.Width(80));
                EditorGUILayout.LabelField("Size", GUILayout.Width(80));
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(250));

            foreach (var asset in _unusedAssets.OrderByDescending(a => a.FileSize))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    asset.Selected = EditorGUILayout.Toggle(asset.Selected, GUILayout.Width(20));

                    if (GUILayout.Button(asset.Name, EditorStyles.linkLabel, GUILayout.Width(250)))
                    {
                        var obj = AssetDatabase.LoadAssetAtPath<Object>(asset.Path);
                        if (obj != null)
                        {
                            Selection.activeObject = obj;
                            EditorGUIUtility.PingObject(obj);
                        }
                    }

                    EditorGUILayout.LabelField(asset.AssetType, GUILayout.Width(80));
                    EditorGUILayout.LabelField(FormatFileSize(asset.FileSize), GUILayout.Width(80));
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void ScanUnusedAssets()
        {
            _unusedAssets.Clear();
            _usedAssets.Clear();
            _showResults = true;
            _totalUnusedSize = 0;

            // シーンファイルを読み込んで参照GUIDを収集
            if (!File.Exists(_sceneToCheck))
            {
                EditorUtility.DisplayDialog("Error", $"Scene not found: {_sceneToCheck}", "OK");
                return;
            }

            var sceneContent = File.ReadAllText(_sceneToCheck);
            var sceneGuids = new HashSet<string>();

            // GUIDを抽出
            var guidMatches = System.Text.RegularExpressions.Regex.Matches(sceneContent, @"guid:\s*([a-f0-9]{32})");
            foreach (System.Text.RegularExpressions.Match match in guidMatches)
            {
                sceneGuids.Add(match.Groups[1].Value);
            }

            Debug.Log($"[UnusedAssetExcluder] Scene references {sceneGuids.Count} unique assets");

            // 除外パターンを構築
            var patterns = new List<string>();
            if (_excludeBridge) patterns.Add("Bridge");
            if (_excludeBoat) patterns.Add("Boat");
            if (_excludePortal) patterns.Add("Portal");
            if (!string.IsNullOrWhiteSpace(_customExcludePattern))
            {
                patterns.AddRange(_customExcludePattern.Split(',').Select(p => p.Trim()));
            }

            if (patterns.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", "除外パターンを1つ以上選択してください。", "OK");
                return;
            }

            // アセットフォルダをスキャン
            var dataPath = Application.dataPath.Replace("\\", "/");
            var fullPath = dataPath + _assetFolderToCheck.Substring(6);

            if (!Directory.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("Error", $"Folder not found: {fullPath}", "OK");
                return;
            }

            var extensions = new[] { "*.fbx", "*.obj", "*.png", "*.jpg", "*.tga", "*.mat", "*.prefab" };

            foreach (var ext in extensions)
            {
                var files = Directory.GetFiles(fullPath, ext, SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);

                    // パターンに一致するかチェック
                    if (!patterns.Any(p => fileName.IndexOf(p, System.StringComparison.OrdinalIgnoreCase) >= 0))
                        continue;

                    var normalizedFile = file.Replace("\\", "/");
                    var assetPath = "Assets" + normalizedFile.Substring(dataPath.Length);

                    // GUIDを取得
                    var guid = AssetDatabase.AssetPathToGUID(assetPath);
                    if (string.IsNullOrEmpty(guid))
                        continue;

                    var fileInfo = new FileInfo(file);
                    var assetInfo = new AssetInfo
                    {
                        Path = assetPath,
                        Name = fileName,
                        FileSize = fileInfo.Length,
                        AssetType = GetAssetType(ext),
                        Selected = true
                    };

                    // シーンで使用されているかチェック
                    if (sceneGuids.Contains(guid))
                    {
                        _usedAssets.Add(assetInfo);
                    }
                    else
                    {
                        _unusedAssets.Add(assetInfo);
                        _totalUnusedSize += fileInfo.Length;
                    }
                }
            }

            Debug.Log($"[UnusedAssetExcluder] Found {_unusedAssets.Count} unused assets ({FormatFileSize(_totalUnusedSize)})");
            Debug.Log($"[UnusedAssetExcluder] Found {_usedAssets.Count} used assets matching patterns");
        }

        private string GetAssetType(string extension)
        {
            return extension.ToLower() switch
            {
                "*.fbx" or "*.obj" => "Mesh",
                "*.png" or "*.jpg" or "*.tga" => "Texture",
                "*.mat" => "Material",
                "*.prefab" => "Prefab",
                _ => "Other"
            };
        }

        private void ExcludeSelectedAssets()
        {
            var selectedAssets = _unusedAssets.Where(a => a.Selected).ToList();
            var excludedCount = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var asset in selectedAssets)
                {
                    var excluded = false;

                    // テクスチャの場合: インポート設定を変更
                    if (asset.AssetType == "Texture")
                    {
                        var importer = AssetImporter.GetAtPath(asset.Path) as TextureImporter;
                        if (importer != null)
                        {
                            // テクスチャを最小サイズに設定
                            var settings = importer.GetDefaultPlatformTextureSettings();
                            settings.maxTextureSize = 32;
                            settings.format = TextureImporterFormat.RGBA32;
                            importer.SetPlatformTextureSettings(settings);
                            importer.SaveAndReimport();
                            excluded = true;
                        }
                    }
                    // メッシュの場合: インポート設定を変更
                    else if (asset.AssetType == "Mesh")
                    {
                        var importer = AssetImporter.GetAtPath(asset.Path) as ModelImporter;
                        if (importer != null)
                        {
                            // メッシュ圧縮を最大に、アニメーションを無効化
                            importer.meshCompression = ModelImporterMeshCompression.High;
                            importer.isReadable = false;
                            importer.importAnimation = false;
                            importer.importBlendShapes = false;
                            importer.SaveAndReimport();
                            excluded = true;
                        }
                    }
                    // マテリアル/プレハブの場合: Addressablesから除外を試みる
                    else
                    {
                        excluded = TryRemoveFromAddressables(asset.Path);
                    }

                    if (excluded)
                    {
                        excludedCount++;
                        Debug.Log($"[UnusedAssetExcluder] Excluded: {asset.Name}");
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Complete",
                $"アセットを除外しました。\n\n" +
                $"- 処理数: {excludedCount}/{selectedAssets.Count}\n\n" +
                "注意: テクスチャとメッシュはインポート設定を変更しました。\n" +
                "完全に除外するには、対応するマテリアルからの参照も解除してください。",
                "OK");

            // リストを更新
            ScanUnusedAssets();
        }

        private bool TryRemoveFromAddressables(string assetPath)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return false;

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            var entry = settings.FindAssetEntry(guid);

            if (entry != null)
            {
                settings.RemoveAssetEntry(guid);
                return true;
            }

            return false;
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
