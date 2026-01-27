using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// マテリアルから不要なテクスチャ参照を解除するウィンドウ
    /// Normal/AO/Roughness等のマップを一括で解除してビルドサイズを削減
    /// </summary>
    public class MaterialTextureRemoverWindow : EditorWindow
    {
        [MenuItem("Tools/Material Texture Remover")]
        public static void ShowWindow()
        {
            var window = GetWindow<MaterialTextureRemoverWindow>("Material Texture Remover");
            window.minSize = new Vector2(550, 650);
        }

        // 設定
        private string _targetFolder = "Assets/StoreAssets/POLYART_Ancient Village";
        private bool _includeSubfolders = true;

        // 解除対象
        private bool _removeNormalMap = true;
        private bool _removeOcclusionMap = true;
        private bool _removeMetallicMap = true;
        private bool _removeDetailMaps = true;
        private bool _removeParallaxMap = true;
        private bool _removeEmissionMap = false; // デフォルトは残す

        // プレビュー
        private List<MaterialInfo> _previewMaterials = new();
        private Vector2 _scrollPosition;
        private bool _showPreview;
        private long _estimatedSavings;

        // シェーダープロパティ名のマッピング
        private static readonly string[] NormalMapProperties = { "_BumpMap", "_NormalMap", "_DetailNormalMap" };
        private static readonly string[] OcclusionMapProperties = { "_OcclusionMap", "_AO", "_AOMap" };
        private static readonly string[] MetallicMapProperties = { "_MetallicGlossMap", "_SpecGlossMap", "_RoughnessMap", "_Metallic" };
        private static readonly string[] DetailMapProperties = { "_DetailAlbedoMap", "_DetailNormalMap", "_DetailMask" };
        private static readonly string[] ParallaxMapProperties = { "_ParallaxMap", "_HeightMap" };
        private static readonly string[] EmissionMapProperties = { "_EmissionMap" };

        private class MaterialInfo
        {
            public string Path;
            public string Name;
            public Material Material;
            public List<TextureReference> TexturesToRemove = new();
            public bool Selected;
        }

        private class TextureReference
        {
            public string PropertyName;
            public string TextureName;
            public string TextureType;
            public long TextureSize;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Material Texture Remover", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "マテリアルからNormal/AO/Roughness等のテクスチャ参照を解除します。\n" +
                "参照を解除されたテクスチャはビルドに含まれなくなり、サイズが削減されます。",
                MessageType.Info);
            EditorGUILayout.Space(5);

            DrawFolderSelection();
            EditorGUILayout.Space(10);

            DrawTextureTypeSelection();
            EditorGUILayout.Space(10);

            DrawActionButtons();

            if (_showPreview)
            {
                EditorGUILayout.Space(10);
                DrawPreviewList();
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
                        var dataPath = Application.dataPath.Replace("\\", "/");
                        if (selected.StartsWith(dataPath))
                        {
                            _targetFolder = "Assets" + selected.Substring(dataPath.Length);
                        }
                    }
                }
            }

            _includeSubfolders = EditorGUILayout.Toggle("Include Subfolders", _includeSubfolders);
        }

        private void DrawTextureTypeSelection()
        {
            EditorGUILayout.LabelField("Texture Types to Remove", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _removeNormalMap = EditorGUILayout.ToggleLeft(
                    "Normal Maps (_BumpMap, _NormalMap) - 推奨削除",
                    _removeNormalMap);

                _removeOcclusionMap = EditorGUILayout.ToggleLeft(
                    "Occlusion Maps (_OcclusionMap, _AO) - 推奨削除",
                    _removeOcclusionMap);

                _removeMetallicMap = EditorGUILayout.ToggleLeft(
                    "Metallic/Roughness Maps (_MetallicGlossMap) - 推奨削除",
                    _removeMetallicMap);

                _removeDetailMaps = EditorGUILayout.ToggleLeft(
                    "Detail Maps (_DetailAlbedoMap, _DetailNormalMap)",
                    _removeDetailMaps);

                _removeParallaxMap = EditorGUILayout.ToggleLeft(
                    "Parallax/Height Maps (_ParallaxMap)",
                    _removeParallaxMap);

                _removeEmissionMap = EditorGUILayout.ToggleLeft(
                    "Emission Maps (_EmissionMap) - 注意: 発光が消えます",
                    _removeEmissionMap);
            }

            // 推定削減量
            EditorGUILayout.Space(5);
            var estimatedReduction = 0;
            if (_removeNormalMap) estimatedReduction += 29;
            if (_removeOcclusionMap) estimatedReduction += 15;
            if (_removeMetallicMap) estimatedReduction += 3;

            EditorGUILayout.LabelField($"推定削減量: 約 {estimatedReduction} MB", EditorStyles.miniLabel);
        }

        private void DrawActionButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan Materials", GUILayout.Height(30)))
                {
                    ScanMaterials();
                }

                using (new EditorGUI.DisabledScope(_previewMaterials.Count == 0))
                {
                    if (GUILayout.Button("Select All", GUILayout.Height(30), GUILayout.Width(80)))
                    {
                        foreach (var mat in _previewMaterials)
                            mat.Selected = true;
                    }

                    if (GUILayout.Button("Deselect All", GUILayout.Height(30), GUILayout.Width(80)))
                    {
                        foreach (var mat in _previewMaterials)
                            mat.Selected = false;
                    }
                }
            }

            EditorGUILayout.Space(5);

            using (new EditorGUI.DisabledScope(_previewMaterials.Count == 0 || !_previewMaterials.Any(m => m.Selected)))
            {
                var selectedCount = _previewMaterials.Count(m => m.Selected);
                var textureCount = _previewMaterials.Where(m => m.Selected).Sum(m => m.TexturesToRemove.Count);
                var buttonText = $"Remove References ({selectedCount} materials, {textureCount} textures)";

                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button(buttonText, GUILayout.Height(35)))
                {
                    if (EditorUtility.DisplayDialog("Confirm Removal",
                            $"以下の参照を解除します:\n\n" +
                            $"- マテリアル数: {selectedCount}\n" +
                            $"- テクスチャ参照数: {textureCount}\n" +
                            $"- 推定削減量: {FormatFileSize(_estimatedSavings)}\n\n" +
                            "この操作は元に戻せません。続行しますか？",
                            "Remove", "Cancel"))
                    {
                        RemoveTextureReferences();
                    }
                }
                GUI.backgroundColor = Color.white;
            }
        }

        private void DrawPreviewList()
        {
            var selectedMaterials = _previewMaterials.Where(m => m.Selected).ToList();
            var totalTextures = selectedMaterials.Sum(m => m.TexturesToRemove.Count);

            EditorGUILayout.LabelField($"Materials with Removable Textures ({_previewMaterials.Count})", EditorStyles.boldLabel);

            if (_estimatedSavings > 0)
            {
                EditorGUILayout.HelpBox(
                    $"選択中: {selectedMaterials.Count} マテリアル, {totalTextures} テクスチャ\n" +
                    $"推定削減量: {FormatFileSize(_estimatedSavings)}",
                    MessageType.Info);
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(300));

            foreach (var mat in _previewMaterials)
            {
                if (mat.TexturesToRemove.Count == 0)
                    continue;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        mat.Selected = EditorGUILayout.Toggle(mat.Selected, GUILayout.Width(20));
                        EditorGUILayout.LabelField(mat.Name, EditorStyles.boldLabel);

                        if (GUILayout.Button("Select", GUILayout.Width(50)))
                        {
                            Selection.activeObject = mat.Material;
                            EditorGUIUtility.PingObject(mat.Material);
                        }
                    }

                    EditorGUI.indentLevel++;
                    foreach (var tex in mat.TexturesToRemove)
                    {
                        var sizeStr = tex.TextureSize > 0 ? $" ({FormatFileSize(tex.TextureSize)})" : "";
                        EditorGUILayout.LabelField($"• {tex.TextureType}: {tex.TextureName}{sizeStr}", EditorStyles.miniLabel);
                    }
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void ScanMaterials()
        {
            _previewMaterials.Clear();
            _showPreview = true;
            _estimatedSavings = 0;

            if (!AssetDatabase.IsValidFolder(_targetFolder))
            {
                EditorUtility.DisplayDialog("Error", $"Folder not found: {_targetFolder}", "OK");
                return;
            }

            // マテリアルを検索
            var searchOption = _includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var dataPath = Application.dataPath.Replace("\\", "/");
            var fullPath = dataPath + _targetFolder.Substring(6); // "Assets" をスキップ

            if (!Directory.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("Error", $"Directory not found: {fullPath}", "OK");
                return;
            }

            var matFiles = Directory.GetFiles(fullPath, "*.mat", searchOption);
            Debug.Log($"[MaterialTextureRemover] Found {matFiles.Length} material files");

            foreach (var file in matFiles)
            {
                var normalizedFile = file.Replace("\\", "/");
                var assetPath = "Assets" + normalizedFile.Substring(dataPath.Length);

                var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                if (material == null)
                    continue;

                var matInfo = new MaterialInfo
                {
                    Path = assetPath,
                    Name = Path.GetFileName(file),
                    Material = material,
                    Selected = true
                };

                // 削除対象テクスチャを検索
                CollectRemovableTextures(material, matInfo);

                if (matInfo.TexturesToRemove.Count > 0)
                {
                    _previewMaterials.Add(matInfo);
                }
            }

            // テクスチャ数でソート
            _previewMaterials = _previewMaterials
                .OrderByDescending(m => m.TexturesToRemove.Count)
                .ToList();

            CalculateEstimatedSavings();

            Debug.Log($"[MaterialTextureRemover] Found {_previewMaterials.Count} materials with removable textures");
        }

        private void CollectRemovableTextures(Material material, MaterialInfo matInfo)
        {
            if (_removeNormalMap)
            {
                foreach (var prop in NormalMapProperties)
                {
                    AddTextureIfExists(material, prop, "Normal", matInfo);
                }
            }

            if (_removeOcclusionMap)
            {
                foreach (var prop in OcclusionMapProperties)
                {
                    AddTextureIfExists(material, prop, "Occlusion", matInfo);
                }
            }

            if (_removeMetallicMap)
            {
                foreach (var prop in MetallicMapProperties)
                {
                    AddTextureIfExists(material, prop, "Metallic/Roughness", matInfo);
                }
            }

            if (_removeDetailMaps)
            {
                foreach (var prop in DetailMapProperties)
                {
                    AddTextureIfExists(material, prop, "Detail", matInfo);
                }
            }

            if (_removeParallaxMap)
            {
                foreach (var prop in ParallaxMapProperties)
                {
                    AddTextureIfExists(material, prop, "Parallax", matInfo);
                }
            }

            if (_removeEmissionMap)
            {
                foreach (var prop in EmissionMapProperties)
                {
                    AddTextureIfExists(material, prop, "Emission", matInfo);
                }
            }
        }

        private void AddTextureIfExists(Material material, string propertyName, string textureType, MaterialInfo matInfo)
        {
            if (!material.HasProperty(propertyName))
                return;

            var texture = material.GetTexture(propertyName);
            if (texture == null)
                return;

            // テクスチャのファイルサイズを取得
            long fileSize = 0;
            var texPath = AssetDatabase.GetAssetPath(texture);
            if (!string.IsNullOrEmpty(texPath))
            {
                var fullTexPath = Path.GetFullPath(texPath);
                if (File.Exists(fullTexPath))
                {
                    fileSize = new FileInfo(fullTexPath).Length;
                }
            }

            matInfo.TexturesToRemove.Add(new TextureReference
            {
                PropertyName = propertyName,
                TextureName = texture.name,
                TextureType = textureType,
                TextureSize = fileSize
            });
        }

        private void CalculateEstimatedSavings()
        {
            // ユニークなテクスチャパスを収集
            var uniqueTextures = new HashSet<string>();
            _estimatedSavings = 0;

            foreach (var mat in _previewMaterials.Where(m => m.Selected))
            {
                foreach (var tex in mat.TexturesToRemove)
                {
                    var texture = mat.Material.GetTexture(tex.PropertyName);
                    if (texture == null)
                        continue;

                    var texPath = AssetDatabase.GetAssetPath(texture);
                    if (string.IsNullOrEmpty(texPath) || uniqueTextures.Contains(texPath))
                        continue;

                    uniqueTextures.Add(texPath);
                    _estimatedSavings += tex.TextureSize;
                }
            }
        }

        private void RemoveTextureReferences()
        {
            var selectedMaterials = _previewMaterials.Where(m => m.Selected).ToList();
            var removedCount = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var matInfo in selectedMaterials)
                {
                    foreach (var tex in matInfo.TexturesToRemove)
                    {
                        if (matInfo.Material.HasProperty(tex.PropertyName))
                        {
                            matInfo.Material.SetTexture(tex.PropertyName, null);
                            removedCount++;

                            // Normal Mapを削除した場合、関連するキーワードも無効化
                            if (tex.PropertyName == "_BumpMap" || tex.PropertyName == "_NormalMap")
                            {
                                matInfo.Material.DisableKeyword("_NORMALMAP");
                                // バンプスケールも0に
                                if (matInfo.Material.HasProperty("_BumpScale"))
                                {
                                    matInfo.Material.SetFloat("_BumpScale", 1f);
                                }
                            }

                            // Occlusion Mapを削除した場合
                            if (tex.PropertyName == "_OcclusionMap")
                            {
                                if (matInfo.Material.HasProperty("_OcclusionStrength"))
                                {
                                    matInfo.Material.SetFloat("_OcclusionStrength", 1f);
                                }
                            }

                            // Metallic/Smoothnessを削除した場合
                            if (tex.PropertyName == "_MetallicGlossMap")
                            {
                                matInfo.Material.DisableKeyword("_METALLICGLOSSMAP");
                            }

                            // Emission Mapを削除した場合
                            if (tex.PropertyName == "_EmissionMap")
                            {
                                matInfo.Material.DisableKeyword("_EMISSION");
                            }
                        }
                    }

                    EditorUtility.SetDirty(matInfo.Material);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Complete",
                $"テクスチャ参照を解除しました。\n\n" +
                $"- 処理マテリアル数: {selectedMaterials.Count}\n" +
                $"- 解除した参照数: {removedCount}\n\n" +
                "ビルドを再実行してサイズ削減を確認してください。",
                "OK");

            // リスト更新
            ScanMaterials();
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
