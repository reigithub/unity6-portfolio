using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// UnityChanのToon/ToonシェーダーマテリアルをGame/ToonLit（WebGL互換）に変換するウィンドウ
    /// 元の色味とテクスチャを引き継ぎつつ、WebGLで動作するシェーダーに変換します
    /// </summary>
    public class UnityChanMaterialConverterWindow : EditorWindow
    {
        [MenuItem("Tools/UnityChan Material Converter")]
        public static void ShowWindow()
        {
            var window = GetWindow<UnityChanMaterialConverterWindow>("UnityChan Material Converter");
            window.minSize = new Vector2(500, 500);
        }

        // タブ
        private int _selectedTab;
        private readonly string[] _tabNames = { "1. Convert Materials", "2. Replace in Prefabs" };

        private string _sourceFolderPath = "Assets/UnityChan/SD Unity-chan 3D Model Data/Materials/UnityChan";
        private string _outputFolderPath = "Assets/UnityChan/SD Unity-chan 3D Model Data/Materials/UnityChan_WebGL";
        private Shader _targetShader;
        private List<MaterialConversionInfo> _materials = new();
        private Vector2 _scrollPosition;
        private bool _scanned;

        // Prefab差し替え用
        private string _prefabFolderPath = "Assets/UnityChan/SD Unity-chan 3D Model Data/Prefabs";
        private List<PrefabReplacementInfo> _prefabs = new();
        private Vector2 _prefabScrollPosition;
        private bool _prefabScanned;
        private Dictionary<Material, Material> _materialMapping = new();

        private class MaterialConversionInfo
        {
            public string SourcePath;
            public string MaterialName;
            public Material SourceMaterial;
            public bool Selected;
            public string Status;
        }

        private class PrefabReplacementInfo
        {
            public string PrefabPath;
            public string PrefabName;
            public GameObject Prefab;
            public bool Selected;
            public int MaterialCount;
            public int ReplacedCount;
            public string Status;
        }

        private void OnEnable()
        {
            _targetShader = Shader.Find("Game/ToonLit");
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("UnityChan Material Converter", EditorStyles.boldLabel);

            EditorGUILayout.Space(5);
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabNames);
            EditorGUILayout.Space(10);

            switch (_selectedTab)
            {
                case 0:
                    DrawMaterialConversionTab();
                    break;
                case 1:
                    DrawPrefabReplacementTab();
                    break;
            }
        }

        private void DrawMaterialConversionTab()
        {
            EditorGUILayout.HelpBox(
                "Toon/Toon シェーダー（WebGL非対応）を Game/ToonLit シェーダー（WebGL対応）に変換します。\n" +
                "元の色味とテクスチャ参照を引き継ぎます。",
                MessageType.Info);

            EditorGUILayout.Space(10);
            DrawSettings();

            EditorGUILayout.Space(10);
            DrawActionButtons();

            if (_scanned)
            {
                EditorGUILayout.Space(10);
                DrawMaterialList();
            }
        }

        private void DrawPrefabReplacementTab()
        {
            EditorGUILayout.HelpBox(
                "UnityChanプレハブのマテリアル参照を、変換済みのWebGL互換マテリアルに自動差し替えします。\n" +
                "Step 1で変換を完了してから実行してください。",
                MessageType.Info);

            EditorGUILayout.Space(10);
            DrawPrefabSettings();

            EditorGUILayout.Space(10);
            DrawPrefabActionButtons();

            if (_prefabScanned)
            {
                EditorGUILayout.Space(10);
                DrawPrefabList();
            }
        }

        private void DrawSettings()
        {
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Source Folder", GUILayout.Width(100));
                _sourceFolderPath = EditorGUILayout.TextField(_sourceFolderPath);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    var selected = EditorUtility.OpenFolderPanel("Select Source Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(selected))
                    {
                        var dataPath = Application.dataPath.Replace("\\", "/");
                        if (selected.StartsWith(dataPath))
                        {
                            _sourceFolderPath = "Assets" + selected.Substring(dataPath.Length);
                        }
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Output Folder", GUILayout.Width(100));
                _outputFolderPath = EditorGUILayout.TextField(_outputFolderPath);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    var selected = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(selected))
                    {
                        var dataPath = Application.dataPath.Replace("\\", "/");
                        if (selected.StartsWith(dataPath))
                        {
                            _outputFolderPath = "Assets" + selected.Substring(dataPath.Length);
                        }
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Target Shader", GUILayout.Width(100));
                _targetShader = (Shader)EditorGUILayout.ObjectField(_targetShader, typeof(Shader), false);
            }

            if (_targetShader == null)
            {
                EditorGUILayout.HelpBox("Game/ToonLit シェーダーが見つかりません。", MessageType.Warning);
            }
        }

        private void DrawActionButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan Materials", GUILayout.Height(30)))
                {
                    ScanMaterials();
                }

                using (new EditorGUI.DisabledScope(!_scanned || _materials.Count == 0))
                {
                    if (GUILayout.Button("Select All", GUILayout.Height(30), GUILayout.Width(80)))
                    {
                        foreach (var mat in _materials)
                            mat.Selected = true;
                    }

                    if (GUILayout.Button("Deselect All", GUILayout.Height(30), GUILayout.Width(80)))
                    {
                        foreach (var mat in _materials)
                            mat.Selected = false;
                    }
                }
            }

            EditorGUILayout.Space(5);

            using (new EditorGUI.DisabledScope(!_scanned || _targetShader == null))
            {
                var selectedCount = 0;
                foreach (var mat in _materials)
                {
                    if (mat.Selected) selectedCount++;
                }

                GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
                if (GUILayout.Button($"Convert Selected Materials ({selectedCount} files)", GUILayout.Height(35)))
                {
                    ConvertMaterials();
                }
                GUI.backgroundColor = Color.white;
            }
        }

        private void DrawMaterialList()
        {
            EditorGUILayout.LabelField($"Materials ({_materials.Count} found)", EditorStyles.boldLabel);

            if (_materials.Count == 0)
            {
                EditorGUILayout.HelpBox("Toon/Toon シェーダーを使用するマテリアルが見つかりませんでした。", MessageType.Info);
                return;
            }

            // ヘッダー
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUILayout.LabelField("", GUILayout.Width(20));
                EditorGUILayout.LabelField("Material Name", GUILayout.Width(200));
                EditorGUILayout.LabelField("Status", GUILayout.Width(150));
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(200));

            foreach (var mat in _materials)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    mat.Selected = EditorGUILayout.Toggle(mat.Selected, GUILayout.Width(20));

                    if (GUILayout.Button(mat.MaterialName, EditorStyles.linkLabel, GUILayout.Width(200)))
                    {
                        if (mat.SourceMaterial != null)
                        {
                            Selection.activeObject = mat.SourceMaterial;
                            EditorGUIUtility.PingObject(mat.SourceMaterial);
                        }
                    }

                    EditorGUILayout.LabelField(mat.Status, GUILayout.Width(150));
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void ScanMaterials()
        {
            _materials.Clear();
            _scanned = true;

            if (!AssetDatabase.IsValidFolder(_sourceFolderPath))
            {
                EditorUtility.DisplayDialog("Error", $"Folder not found: {_sourceFolderPath}", "OK");
                return;
            }

            var guids = AssetDatabase.FindAssets("t:Material", new[] { _sourceFolderPath });

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (material == null) continue;

                // Toon/Toon シェーダーを使用しているかチェック
                if (material.shader != null && material.shader.name == "Toon/Toon")
                {
                    var outputPath = _outputFolderPath + "/" + Path.GetFileName(path);
                    var exists = AssetDatabase.LoadAssetAtPath<Material>(outputPath) != null;

                    _materials.Add(new MaterialConversionInfo
                    {
                        SourcePath = path,
                        MaterialName = material.name,
                        SourceMaterial = material,
                        Selected = true,
                        Status = exists ? "Output exists" : "Ready"
                    });
                }
            }

            Debug.Log($"[UnityChanMaterialConverter] Found {_materials.Count} Toon/Toon materials");
        }

        private void ConvertMaterials()
        {
            if (_targetShader == null)
            {
                EditorUtility.DisplayDialog("Error", "Target shader is not set.", "OK");
                return;
            }

            // 出力フォルダを作成
            if (!AssetDatabase.IsValidFolder(_outputFolderPath))
            {
                var parentFolder = Path.GetDirectoryName(_outputFolderPath)?.Replace("\\", "/");
                var folderName = Path.GetFileName(_outputFolderPath);
                if (!string.IsNullOrEmpty(parentFolder) && !string.IsNullOrEmpty(folderName))
                {
                    AssetDatabase.CreateFolder(parentFolder, folderName);
                }
            }

            var convertedCount = 0;

            foreach (var matInfo in _materials)
            {
                if (!matInfo.Selected) continue;

                var newMaterial = ConvertMaterial(matInfo.SourceMaterial);
                if (newMaterial != null)
                {
                    var outputPath = _outputFolderPath + "/" + matInfo.MaterialName + ".mat";

                    // 既存のマテリアルがあれば削除
                    var existing = AssetDatabase.LoadAssetAtPath<Material>(outputPath);
                    if (existing != null)
                    {
                        AssetDatabase.DeleteAsset(outputPath);
                    }

                    AssetDatabase.CreateAsset(newMaterial, outputPath);
                    matInfo.Status = "Converted";
                    convertedCount++;
                    Debug.Log($"[UnityChanMaterialConverter] Converted: {matInfo.MaterialName}");
                }
                else
                {
                    matInfo.Status = "Failed";
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Complete",
                $"マテリアル変換が完了しました。\n\n" +
                $"変換数: {convertedCount}\n" +
                $"出力先: {_outputFolderPath}\n\n" +
                "注意: キャラクターのマテリアル参照を新しいマテリアルに差し替えてください。",
                "OK");
        }

        private Material ConvertMaterial(Material source)
        {
            if (source == null || _targetShader == null) return null;

            var newMat = new Material(_targetShader);
            newMat.name = source.name;

            // テクスチャをコピー
            if (source.HasProperty("_MainTex"))
            {
                var mainTex = source.GetTexture("_MainTex");
                if (mainTex != null)
                {
                    newMat.SetTexture("_BaseMap", mainTex);
                }
            }

            // ベースカラー
            if (source.HasProperty("_BaseColor"))
            {
                newMat.SetColor("_BaseColor", source.GetColor("_BaseColor"));
            }
            else if (source.HasProperty("_Color"))
            {
                newMat.SetColor("_BaseColor", source.GetColor("_Color"));
            }

            // シェードカラー（1st Shade Color を使用）
            if (source.HasProperty("_1st_ShadeColor"))
            {
                var shadeColor = source.GetColor("_1st_ShadeColor");
                newMat.SetColor("_ShadeColor", shadeColor);
            }

            // シェードのしきい値と滑らかさ
            if (source.HasProperty("_BaseColor_Step"))
            {
                // UTS の BaseColor_Step (0-1) を ToonLit の ShadeThreshold に変換
                // UTS: 値が大きいほど影が少ない
                // ToonLit: 値が大きいほど影が多い
                var step = source.GetFloat("_BaseColor_Step");
                newMat.SetFloat("_ShadeThreshold", 1f - step);
            }

            if (source.HasProperty("_BaseShade_Feather"))
            {
                var feather = source.GetFloat("_BaseShade_Feather");
                // Feather を Smoothness に変換（0.0001-1 → 0-0.5）
                newMat.SetFloat("_ShadeSmoothness", Mathf.Clamp(feather * 0.5f, 0f, 0.5f));
            }

            // リムライト
            if (source.HasProperty("_RimLightColor"))
            {
                var rimColor = source.GetColor("_RimLightColor");
                // リムライトが有効かチェック
                var rimEnabled = source.HasProperty("_RimLight") && source.GetFloat("_RimLight") > 0;
                if (rimEnabled)
                {
                    newMat.SetColor("_RimColor", rimColor);
                    if (source.HasProperty("_RimLight_Power"))
                    {
                        var power = source.GetFloat("_RimLight_Power");
                        newMat.SetFloat("_RimPower", Mathf.Lerp(1f, 10f, 1f - power));
                    }
                }
                else
                {
                    // リムライト無効化（透明色）
                    newMat.SetColor("_RimColor", new Color(1, 1, 1, 0));
                }
            }

            // アウトライン
            if (source.HasProperty("_Outline_Color"))
            {
                newMat.SetColor("_OutlineColor", source.GetColor("_Outline_Color"));
            }

            if (source.HasProperty("_Outline_Width"))
            {
                var width = source.GetFloat("_Outline_Width");
                newMat.SetFloat("_OutlineWidth", width);
            }

            // カリングモード
            if (source.HasProperty("_CullMode"))
            {
                newMat.SetFloat("_Cull", source.GetFloat("_CullMode"));
            }

            return newMat;
        }

        #region Prefab Replacement

        private void DrawPrefabSettings()
        {
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Prefab Folder", GUILayout.Width(100));
                _prefabFolderPath = EditorGUILayout.TextField(_prefabFolderPath);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    var selected = EditorUtility.OpenFolderPanel("Select Prefab Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(selected))
                    {
                        var dataPath = Application.dataPath.Replace("\\", "/");
                        if (selected.StartsWith(dataPath))
                        {
                            _prefabFolderPath = "Assets" + selected.Substring(dataPath.Length);
                        }
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Source Folder", GUILayout.Width(100));
                EditorGUILayout.LabelField(_sourceFolderPath, EditorStyles.miniLabel);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Output Folder", GUILayout.Width(100));
                EditorGUILayout.LabelField(_outputFolderPath, EditorStyles.miniLabel);
            }
        }

        private void DrawPrefabActionButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan Prefabs", GUILayout.Height(30)))
                {
                    ScanPrefabs();
                }

                using (new EditorGUI.DisabledScope(!_prefabScanned || _prefabs.Count == 0))
                {
                    if (GUILayout.Button("Select All", GUILayout.Height(30), GUILayout.Width(80)))
                    {
                        foreach (var p in _prefabs)
                            p.Selected = true;
                    }

                    if (GUILayout.Button("Deselect All", GUILayout.Height(30), GUILayout.Width(80)))
                    {
                        foreach (var p in _prefabs)
                            p.Selected = false;
                    }
                }
            }

            EditorGUILayout.Space(5);

            using (new EditorGUI.DisabledScope(!_prefabScanned || _prefabs.Count == 0))
            {
                var selectedCount = _prefabs.Count(p => p.Selected);
                var totalMaterials = _prefabs.Where(p => p.Selected).Sum(p => p.MaterialCount);

                GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
                if (GUILayout.Button($"Replace Materials in Selected Prefabs ({selectedCount} prefabs, {totalMaterials} materials)", GUILayout.Height(35)))
                {
                    if (EditorUtility.DisplayDialog("Confirm Replacement",
                        $"以下のプレハブのマテリアルを差し替えます:\n\n" +
                        $"- プレハブ数: {selectedCount}\n" +
                        $"- マテリアル参照数: {totalMaterials}\n\n" +
                        "この操作はプレハブを直接変更します。\n続行しますか？",
                        "Replace", "Cancel"))
                    {
                        ReplaceMaterialsInPrefabs();
                    }
                }
                GUI.backgroundColor = Color.white;
            }
        }

        private void DrawPrefabList()
        {
            EditorGUILayout.LabelField($"Prefabs ({_prefabs.Count} found)", EditorStyles.boldLabel);

            if (_prefabs.Count == 0)
            {
                EditorGUILayout.HelpBox("UnityChanプレハブが見つかりませんでした。", MessageType.Info);
                return;
            }

            // マッピング情報
            if (_materialMapping.Count > 0)
            {
                EditorGUILayout.LabelField($"Material Mapping: {_materialMapping.Count} pairs found", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.HelpBox("マテリアルマッピングが見つかりません。Step 1でマテリアル変換を先に実行してください。", MessageType.Warning);
            }

            // ヘッダー
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUILayout.LabelField("", GUILayout.Width(20));
                EditorGUILayout.LabelField("Prefab Name", GUILayout.Width(200));
                EditorGUILayout.LabelField("Materials", GUILayout.Width(80));
                EditorGUILayout.LabelField("Status", GUILayout.Width(120));
            }

            _prefabScrollPosition = EditorGUILayout.BeginScrollView(_prefabScrollPosition, GUILayout.Height(200));

            foreach (var prefab in _prefabs)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    prefab.Selected = EditorGUILayout.Toggle(prefab.Selected, GUILayout.Width(20));

                    if (GUILayout.Button(prefab.PrefabName, EditorStyles.linkLabel, GUILayout.Width(200)))
                    {
                        if (prefab.Prefab != null)
                        {
                            Selection.activeObject = prefab.Prefab;
                            EditorGUIUtility.PingObject(prefab.Prefab);
                        }
                    }

                    EditorGUILayout.LabelField(prefab.MaterialCount.ToString(), GUILayout.Width(80));
                    EditorGUILayout.LabelField(prefab.Status, GUILayout.Width(120));
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void ScanPrefabs()
        {
            _prefabs.Clear();
            _prefabScanned = true;

            // マテリアルマッピングを構築
            BuildMaterialMapping();

            if (!AssetDatabase.IsValidFolder(_prefabFolderPath))
            {
                EditorUtility.DisplayDialog("Error", $"Folder not found: {_prefabFolderPath}", "OK");
                return;
            }

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { _prefabFolderPath });

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null) continue;

                // Rendererを持つか確認
                var renderers = prefab.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) continue;

                // Toon/Toon マテリアルを使用しているかチェック
                var toonMaterialCount = 0;
                foreach (var renderer in renderers)
                {
                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mat != null && mat.shader != null && mat.shader.name == "Toon/Toon")
                        {
                            toonMaterialCount++;
                        }
                    }
                }

                if (toonMaterialCount > 0)
                {
                    _prefabs.Add(new PrefabReplacementInfo
                    {
                        PrefabPath = path,
                        PrefabName = prefab.name,
                        Prefab = prefab,
                        Selected = true,
                        MaterialCount = toonMaterialCount,
                        ReplacedCount = 0,
                        Status = _materialMapping.Count > 0 ? "Ready" : "No mapping"
                    });
                }
            }

            Debug.Log($"[UnityChanMaterialConverter] Found {_prefabs.Count} prefabs with Toon/Toon materials");
        }

        private void BuildMaterialMapping()
        {
            _materialMapping.Clear();

            if (!AssetDatabase.IsValidFolder(_sourceFolderPath) || !AssetDatabase.IsValidFolder(_outputFolderPath))
            {
                return;
            }

            // ソースフォルダのマテリアルを取得
            var sourceGuids = AssetDatabase.FindAssets("t:Material", new[] { _sourceFolderPath });

            foreach (var guid in sourceGuids)
            {
                var sourcePath = AssetDatabase.GUIDToAssetPath(guid);
                var sourceMat = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);

                if (sourceMat == null || sourceMat.shader == null || sourceMat.shader.name != "Toon/Toon")
                    continue;

                // 対応する出力マテリアルを探す
                var outputPath = _outputFolderPath + "/" + sourceMat.name + ".mat";
                var outputMat = AssetDatabase.LoadAssetAtPath<Material>(outputPath);

                if (outputMat != null)
                {
                    _materialMapping[sourceMat] = outputMat;
                }
            }

            Debug.Log($"[UnityChanMaterialConverter] Built material mapping: {_materialMapping.Count} pairs");
        }

        private void ReplaceMaterialsInPrefabs()
        {
            if (_materialMapping.Count == 0)
            {
                EditorUtility.DisplayDialog("Error",
                    "マテリアルマッピングがありません。\nStep 1でマテリアル変換を先に実行してください。",
                    "OK");
                return;
            }

            var totalReplaced = 0;
            var prefabsModified = 0;

            foreach (var prefabInfo in _prefabs)
            {
                if (!prefabInfo.Selected) continue;

                // プレハブを編集モードで開く
                var prefabPath = prefabInfo.PrefabPath;
                var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

                if (prefabRoot == null)
                {
                    prefabInfo.Status = "Failed to load";
                    continue;
                }

                var replacedInThisPrefab = 0;

                try
                {
                    var renderers = prefabRoot.GetComponentsInChildren<Renderer>(true);

                    foreach (var renderer in renderers)
                    {
                        var materials = renderer.sharedMaterials;
                        var modified = false;

                        for (int i = 0; i < materials.Length; i++)
                        {
                            if (materials[i] != null && _materialMapping.TryGetValue(materials[i], out var newMat))
                            {
                                materials[i] = newMat;
                                modified = true;
                                replacedInThisPrefab++;
                            }
                        }

                        if (modified)
                        {
                            renderer.sharedMaterials = materials;
                        }
                    }

                    // 変更を保存
                    if (replacedInThisPrefab > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                        prefabsModified++;
                    }

                    prefabInfo.ReplacedCount = replacedInThisPrefab;
                    prefabInfo.Status = replacedInThisPrefab > 0 ? $"Replaced {replacedInThisPrefab}" : "No changes";
                    totalReplaced += replacedInThisPrefab;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }

                Debug.Log($"[UnityChanMaterialConverter] {prefabInfo.PrefabName}: Replaced {replacedInThisPrefab} materials");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Complete",
                $"マテリアル差し替えが完了しました。\n\n" +
                $"- 変更したプレハブ数: {prefabsModified}\n" +
                $"- 差し替えたマテリアル参照数: {totalReplaced}",
                "OK");
        }

        #endregion
    }
}
