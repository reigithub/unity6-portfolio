using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Skybox/Cubemapシェーダーを使用するマテリアルをビルドから除外するウィンドウ
    /// WebGLでコンパイルエラーが発生するSkybox/Cubemapシェーダーの問題を解決します
    /// </summary>
    public class SkyboxCubemapExcluderWindow : EditorWindow
    {
        [MenuItem("Tools/Skybox Cubemap Excluder")]
        public static void ShowWindow()
        {
            var window = GetWindow<SkyboxCubemapExcluderWindow>("Skybox Cubemap Excluder");
            window.minSize = new Vector2(550, 400);
        }

        private List<MaterialInfo> _cubemapMaterials = new();
        private Vector2 _scrollPosition;
        private bool _scanned;

        // Skybox/Cubemap の Built-in Shader ID
        private const int SkyboxCubemapFileID = 103;
        private const string BuiltinShaderGuid = "0000000000000000f000000000000000";

        private class MaterialInfo
        {
            public string Path;
            public string Name;
            public bool IsUsedInScene;
            public bool Selected;
            public string Status;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Skybox/Cubemap Excluder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Skybox/Cubemap シェーダーはWebGLでコンパイルエラーが発生します。\n" +
                "このツールで未使用のSkybox/Cubemapマテリアルを検出し、ビルドから除外します。",
                MessageType.Info);

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Scan Skybox/Cubemap Materials", GUILayout.Height(30)))
            {
                ScanMaterials();
            }

            if (_scanned)
            {
                EditorGUILayout.Space(10);
                DrawMaterialList();

                EditorGUILayout.Space(10);
                DrawActionButtons();
            }
        }

        private void ScanMaterials()
        {
            _cubemapMaterials.Clear();
            _scanned = true;

            // シーンで使用されているマテリアルGUIDを収集
            var usedMaterialGuids = new HashSet<string>();
            var sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/ProjectAssets" });

            foreach (var sceneGuid in sceneGuids)
            {
                var scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
                var dependencies = AssetDatabase.GetDependencies(scenePath, true);

                foreach (var dep in dependencies)
                {
                    if (dep.EndsWith(".mat"))
                    {
                        var matGuid = AssetDatabase.AssetPathToGUID(dep);
                        usedMaterialGuids.Add(matGuid);
                    }
                }
            }

            // すべてのマテリアルをスキャン
            var materialGuids = AssetDatabase.FindAssets("t:Material");

            foreach (var guid in materialGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                // エディタフォルダはスキップ
                if (path.Contains("/Editor/")) continue;

                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null) continue;

                // Skybox/Cubemap シェーダーかチェック
                if (IsSkyboxCubemapShader(path))
                {
                    var isUsed = usedMaterialGuids.Contains(guid);

                    _cubemapMaterials.Add(new MaterialInfo
                    {
                        Path = path,
                        Name = material.name,
                        IsUsedInScene = isUsed,
                        Selected = !isUsed, // 未使用のものをデフォルトで選択
                        Status = isUsed ? "Used in Scene" : "Unused"
                    });
                }
            }

            Debug.Log($"[SkyboxCubemapExcluder] Found {_cubemapMaterials.Count} Skybox/Cubemap materials");
        }

        private bool IsSkyboxCubemapShader(string materialPath)
        {
            // マテリアルファイルを直接読んでシェーダー参照をチェック
            try
            {
                var content = File.ReadAllText(materialPath);
                // fileID: 103, guid: 0000000000000000f000000000000000 がSkybox/Cubemap
                return content.Contains($"fileID: {SkyboxCubemapFileID}, guid: {BuiltinShaderGuid}");
            }
            catch
            {
                return false;
            }
        }

        private void DrawMaterialList()
        {
            var unusedCount = 0;
            foreach (var mat in _cubemapMaterials)
            {
                if (!mat.IsUsedInScene) unusedCount++;
            }

            EditorGUILayout.LabelField($"Skybox/Cubemap Materials ({_cubemapMaterials.Count} found, {unusedCount} unused)", EditorStyles.boldLabel);

            if (_cubemapMaterials.Count == 0)
            {
                EditorGUILayout.HelpBox("Skybox/Cubemap マテリアルは見つかりませんでした。", MessageType.Info);
                return;
            }

            // ヘッダー
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUILayout.LabelField("", GUILayout.Width(20));
                EditorGUILayout.LabelField("Material", GUILayout.Width(200));
                EditorGUILayout.LabelField("Status", GUILayout.Width(100));
                EditorGUILayout.LabelField("Path", GUILayout.Width(200));
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(200));

            foreach (var mat in _cubemapMaterials)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    // 使用中のものは選択不可
                    using (new EditorGUI.DisabledScope(mat.IsUsedInScene))
                    {
                        mat.Selected = EditorGUILayout.Toggle(mat.Selected, GUILayout.Width(20));
                    }

                    // マテリアル名（クリックで選択）
                    var style = mat.IsUsedInScene ? EditorStyles.label : EditorStyles.linkLabel;
                    if (GUILayout.Button(mat.Name, style, GUILayout.Width(200)))
                    {
                        var obj = AssetDatabase.LoadAssetAtPath<Material>(mat.Path);
                        if (obj != null)
                        {
                            Selection.activeObject = obj;
                            EditorGUIUtility.PingObject(obj);
                        }
                    }

                    // ステータス
                    var statusColor = mat.IsUsedInScene ? Color.yellow : Color.green;
                    GUI.contentColor = statusColor;
                    EditorGUILayout.LabelField(mat.Status, GUILayout.Width(100));
                    GUI.contentColor = Color.white;

                    // パス（短縮表示）
                    var shortPath = mat.Path;
                    if (shortPath.Length > 40)
                    {
                        shortPath = "..." + shortPath.Substring(shortPath.Length - 37);
                    }
                    EditorGUILayout.LabelField(shortPath, EditorStyles.miniLabel, GUILayout.Width(200));
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawActionButtons()
        {
            var selectedCount = 0;
            foreach (var mat in _cubemapMaterials)
            {
                if (mat.Selected) selectedCount++;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select All Unused", GUILayout.Height(25)))
                {
                    foreach (var mat in _cubemapMaterials)
                    {
                        if (!mat.IsUsedInScene) mat.Selected = true;
                    }
                }

                if (GUILayout.Button("Deselect All", GUILayout.Height(25)))
                {
                    foreach (var mat in _cubemapMaterials)
                    {
                        mat.Selected = false;
                    }
                }
            }

            EditorGUILayout.Space(5);

            using (new EditorGUI.DisabledScope(selectedCount == 0))
            {
                // オプション1: シェーダーを変更
                GUI.backgroundColor = new Color(0.8f, 1f, 0.8f);
                if (GUILayout.Button($"Convert to Skybox/6 Sided ({selectedCount} materials)", GUILayout.Height(30)))
                {
                    if (EditorUtility.DisplayDialog("Confirm Conversion",
                        $"{selectedCount}個のマテリアルのシェーダーを Skybox/6 Sided に変更します。\n\n" +
                        "この操作により、キューブマップテクスチャの参照は失われます。\n" +
                        "続行しますか？",
                        "Convert", "Cancel"))
                    {
                        ConvertToSixSided();
                    }
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.Space(5);

                // オプション2: 削除
                GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
                if (GUILayout.Button($"Delete Selected Materials ({selectedCount} materials)", GUILayout.Height(30)))
                {
                    if (EditorUtility.DisplayDialog("Confirm Deletion",
                        $"{selectedCount}個のマテリアルを削除します。\n\n" +
                        "この操作は元に戻せません。\n" +
                        "続行しますか？",
                        "Delete", "Cancel"))
                    {
                        DeleteSelectedMaterials();
                    }
                }
                GUI.backgroundColor = Color.white;
            }
        }

        private void ConvertToSixSided()
        {
            var converted = 0;
            var sixSidedShader = Shader.Find("Skybox/6 Sided");

            if (sixSidedShader == null)
            {
                EditorUtility.DisplayDialog("Error", "Skybox/6 Sided シェーダーが見つかりません。", "OK");
                return;
            }

            foreach (var matInfo in _cubemapMaterials)
            {
                if (!matInfo.Selected) continue;

                var material = AssetDatabase.LoadAssetAtPath<Material>(matInfo.Path);
                if (material != null)
                {
                    material.shader = sixSidedShader;
                    EditorUtility.SetDirty(material);
                    matInfo.Status = "Converted";
                    converted++;
                    Debug.Log($"[SkyboxCubemapExcluder] Converted: {matInfo.Name}");
                }
            }

            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Complete",
                $"{converted}個のマテリアルを Skybox/6 Sided に変換しました。\n\n" +
                "注意: テクスチャは再設定が必要です。",
                "OK");

            // 再スキャン
            ScanMaterials();
        }

        private void DeleteSelectedMaterials()
        {
            var deleted = 0;
            var failedPaths = new List<string>();

            foreach (var matInfo in _cubemapMaterials)
            {
                if (!matInfo.Selected) continue;

                if (AssetDatabase.DeleteAsset(matInfo.Path))
                {
                    deleted++;
                    Debug.Log($"[SkyboxCubemapExcluder] Deleted: {matInfo.Path}");
                }
                else
                {
                    failedPaths.Add(matInfo.Path);
                }
            }

            AssetDatabase.Refresh();

            var message = $"{deleted}個のマテリアルを削除しました。";
            if (failedPaths.Count > 0)
            {
                message += $"\n\n削除に失敗: {failedPaths.Count}個";
            }

            EditorUtility.DisplayDialog("Complete", message, "OK");

            // 再スキャン
            ScanMaterials();
        }
    }
}
