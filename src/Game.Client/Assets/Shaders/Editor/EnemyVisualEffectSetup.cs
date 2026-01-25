using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Game.MVP.Survivor.Enemy;

namespace Game.Editor.Shaders
{
    /// <summary>
    /// 敵プレハブのビジュアルエフェクト一括設定ツール
    /// - EnemyVisualEffectController の追加
    /// - マテリアルシェーダーの変換
    /// </summary>
    public class EnemyVisualEffectSetup : EditorWindow
    {
        private Vector2 _scrollPosition;
        private List<PrefabInfo> _prefabInfos = new();
        private bool _addVisualEffectController = true;
        private bool _convertMaterials = true;
        private bool _autoDetectRenderers = true;
        private Shader _targetShader;
        private Texture2D _noiseTexture;

        // 設定
        private Color _hitFlashColor = Color.white;
        private float _hitFlashDuration = 0.15f;
        private float _dissolveDuration = 1.2f;
        private float _dissolveDelay = 0.3f;
        private Color _dissolveEdgeColor = new Color(1f, 0.5f, 0f, 1f);

        private class PrefabInfo
        {
            public GameObject Prefab;
            public string Path;
            public bool HasVisualEffectController;
            public bool HasSurvivorEnemyController;
            public int MaterialCount;
            public List<Material> Materials = new();
            public bool Selected = true;
        }

        [MenuItem("Tools/Game/Enemy Visual Effect Setup")]
        public static void ShowWindow()
        {
            var window = GetWindow<EnemyVisualEffectSetup>("Enemy VFX Setup");
            window.minSize = new Vector2(500, 600);
        }

        private void OnEnable()
        {
            _targetShader = Shader.Find("Game/Character/CharacterLit");
            _noiseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/StoreAssets/JMO Assets/Cartoon FX Remaster/CFXR Assets/Graphics/cfxr noise clouds big.png");
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("敵ビジュアルエフェクト一括設定", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox(
                "敵プレハブに EnemyVisualEffectController を追加し、\n" +
                "マテリアルをエフェクト対応シェーダーに変換します。",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // 設定セクション
            DrawSettingsSection();

            EditorGUILayout.Space(10);

            // 検索ボタン
            if (GUILayout.Button("敵プレハブを検索", GUILayout.Height(30)))
            {
                SearchEnemyPrefabs();
            }

            EditorGUILayout.Space(10);

            // 結果表示
            if (_prefabInfos.Count > 0)
            {
                DrawPrefabList();

                EditorGUILayout.Space(10);

                // 実行ボタン
                DrawExecuteButtons();
            }
        }

        private void DrawSettingsSection()
        {
            EditorGUILayout.LabelField("設定", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _addVisualEffectController = EditorGUILayout.Toggle(
                    "EnemyVisualEffectController を追加",
                    _addVisualEffectController);

                if (_addVisualEffectController)
                {
                    EditorGUI.indentLevel++;
                    _autoDetectRenderers = EditorGUILayout.Toggle("Renderer を自動検出", _autoDetectRenderers);

                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("ヒットフラッシュ設定", EditorStyles.miniLabel);
                    _hitFlashColor = EditorGUILayout.ColorField("Flash Color", _hitFlashColor);
                    _hitFlashDuration = EditorGUILayout.Slider("Duration", _hitFlashDuration, 0.05f, 0.5f);

                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("ディゾルブ設定", EditorStyles.miniLabel);
                    _dissolveDuration = EditorGUILayout.Slider("Duration", _dissolveDuration, 0.5f, 3f);
                    _dissolveDelay = EditorGUILayout.Slider("Delay", _dissolveDelay, 0f, 1f);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space(5);

                _convertMaterials = EditorGUILayout.Toggle(
                    "マテリアルシェーダーを変換",
                    _convertMaterials);

                if (_convertMaterials)
                {
                    EditorGUI.indentLevel++;
                    _targetShader = (Shader)EditorGUILayout.ObjectField(
                        "変換先シェーダー",
                        _targetShader,
                        typeof(Shader),
                        false);

                    _noiseTexture = (Texture2D)EditorGUILayout.ObjectField(
                        "ノイズテクスチャ",
                        _noiseTexture,
                        typeof(Texture2D),
                        false);
                    EditorGUI.indentLevel--;
                }
            }
        }

        private void DrawPrefabList()
        {
            int selectedCount = _prefabInfos.Count(p => p.Selected);
            int needsController = _prefabInfos.Count(p => p.Selected && !p.HasVisualEffectController);
            int needsMaterialConvert = _prefabInfos.Count(p => p.Selected && p.MaterialCount > 0);

            EditorGUILayout.LabelField(
                $"検出されたプレハブ: {_prefabInfos.Count}件 (選択: {selectedCount}件)",
                EditorStyles.boldLabel);

            EditorGUILayout.LabelField(
                $"  - Controller追加が必要: {needsController}件",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"  - マテリアル変換対象: {needsMaterialConvert}件",
                EditorStyles.miniLabel);

            EditorGUILayout.Space(5);

            // 選択ボタン
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("すべて選択", GUILayout.Width(100)))
                {
                    foreach (var info in _prefabInfos) info.Selected = true;
                }
                if (GUILayout.Button("すべて解除", GUILayout.Width(100)))
                {
                    foreach (var info in _prefabInfos) info.Selected = false;
                }
                if (GUILayout.Button("未設定のみ選択", GUILayout.Width(120)))
                {
                    foreach (var info in _prefabInfos)
                    {
                        info.Selected = !info.HasVisualEffectController;
                    }
                }
            }

            EditorGUILayout.Space(5);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(200));

            foreach (var info in _prefabInfos)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    info.Selected = EditorGUILayout.Toggle(info.Selected, GUILayout.Width(20));

                    // アイコン表示
                    var icon = info.HasVisualEffectController
                        ? EditorGUIUtility.IconContent("d_greenLight")
                        : EditorGUIUtility.IconContent("d_orangeLight");
                    GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(18));

                    EditorGUILayout.ObjectField(info.Prefab, typeof(GameObject), false);

                    string status = info.HasVisualEffectController ? "設定済" : "未設定";
                    EditorGUILayout.LabelField(status, GUILayout.Width(50));

                    EditorGUILayout.LabelField($"Mat: {info.MaterialCount}", GUILayout.Width(50));
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawExecuteButtons()
        {
            int selectedCount = _prefabInfos.Count(p => p.Selected);

            using (new EditorGUI.DisabledGroupScope(selectedCount == 0))
            {
                GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
                if (GUILayout.Button($"選択したプレハブを設定 ({selectedCount}件)", GUILayout.Height(35)))
                {
                    ExecuteSetup();
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.Space(5);

            if (GUILayout.Button("すべて設定（確認なし）", GUILayout.Height(25)))
            {
                foreach (var info in _prefabInfos) info.Selected = true;
                ExecuteSetup();
            }
        }

        private void SearchEnemyPrefabs()
        {
            _prefabInfos.Clear();

            string[] searchPaths = new[]
            {
                "Assets/ProjectAssets/Survivor/Prefabs/Enemy"
            };

            foreach (var searchPath in searchPaths)
            {
                if (!AssetDatabase.IsValidFolder(searchPath)) continue;

                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { searchPath });

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                    if (prefab == null) continue;

                    // SurvivorEnemyController を持っているかチェック
                    var enemyController = prefab.GetComponent<SurvivorEnemyController>();
                    if (enemyController == null) continue;

                    var info = new PrefabInfo
                    {
                        Prefab = prefab,
                        Path = path,
                        HasSurvivorEnemyController = true,
                        HasVisualEffectController = prefab.GetComponent<EnemyVisualEffectController>() != null
                    };

                    // マテリアル収集
                    var renderers = prefab.GetComponentsInChildren<Renderer>(true);
                    var materials = new HashSet<Material>();

                    foreach (var renderer in renderers)
                    {
                        foreach (var mat in renderer.sharedMaterials)
                        {
                            if (mat != null && mat.shader != _targetShader)
                            {
                                materials.Add(mat);
                            }
                        }
                    }

                    info.Materials = materials.ToList();
                    info.MaterialCount = info.Materials.Count;

                    _prefabInfos.Add(info);
                }
            }

            Debug.Log($"[EnemyVisualEffectSetup] {_prefabInfos.Count}件の敵プレハブを検出しました");
        }

        private void ExecuteSetup()
        {
            var selectedPrefabs = _prefabInfos.Where(p => p.Selected).ToList();
            if (selectedPrefabs.Count == 0) return;

            int controllersAdded = 0;
            int materialsConverted = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var info in selectedPrefabs)
                {
                    // プレハブを編集モードで開く
                    string prefabPath = info.Path;
                    var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

                    bool modified = false;

                    // EnemyVisualEffectController を追加
                    if (_addVisualEffectController && !info.HasVisualEffectController)
                    {
                        var controller = prefabRoot.AddComponent<EnemyVisualEffectController>();

                        // SerializedObject で設定
                        var so = new SerializedObject(controller);

                        so.FindProperty("_hitFlashColor").colorValue = _hitFlashColor;
                        so.FindProperty("_hitFlashDuration").floatValue = _hitFlashDuration;
                        so.FindProperty("_dissolveDuration").floatValue = _dissolveDuration;
                        so.FindProperty("_dissolveDelay").floatValue = _dissolveDelay;

                        // Renderer を自動検出
                        if (_autoDetectRenderers)
                        {
                            var renderers = prefabRoot.GetComponentsInChildren<Renderer>(true);
                            var renderersProp = so.FindProperty("_targetRenderers");
                            renderersProp.arraySize = renderers.Length;
                            for (int i = 0; i < renderers.Length; i++)
                            {
                                renderersProp.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
                            }
                        }

                        so.ApplyModifiedPropertiesWithoutUndo();

                        controllersAdded++;
                        modified = true;

                        Debug.Log($"[EnemyVisualEffectSetup] Controller追加: {info.Prefab.name}");
                    }

                    // マテリアル変換
                    if (_convertMaterials && info.Materials.Count > 0)
                    {
                        foreach (var material in info.Materials)
                        {
                            if (ConvertMaterial(material))
                            {
                                materialsConverted++;
                            }
                        }
                        modified = true;
                    }

                    // プレハブを保存
                    if (modified)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    }

                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            // 結果表示
            string message = "";
            if (_addVisualEffectController)
            {
                message += $"Controller追加: {controllersAdded}件\n";
            }
            if (_convertMaterials)
            {
                message += $"マテリアル変換: {materialsConverted}件";
            }

            EditorUtility.DisplayDialog("完了", message, "OK");

            // リスト更新
            SearchEnemyPrefabs();
        }

        private bool ConvertMaterial(Material material)
        {
            if (material == null || _targetShader == null) return false;
            if (material.shader == _targetShader) return false;

            // 現在のプロパティを保存
            var savedTextures = new Dictionary<string, Texture>();
            var savedColors = new Dictionary<string, Color>();
            var savedFloats = new Dictionary<string, float>();

            var oldShader = material.shader;
            int propertyCount = ShaderUtil.GetPropertyCount(oldShader);

            for (int i = 0; i < propertyCount; i++)
            {
                string propName = ShaderUtil.GetPropertyName(oldShader, i);
                var propType = ShaderUtil.GetPropertyType(oldShader, i);

                switch (propType)
                {
                    case ShaderUtil.ShaderPropertyType.TexEnv:
                        var tex = material.GetTexture(propName);
                        if (tex != null) savedTextures[propName] = tex;
                        break;
                    case ShaderUtil.ShaderPropertyType.Color:
                        savedColors[propName] = material.GetColor(propName);
                        break;
                    case ShaderUtil.ShaderPropertyType.Float:
                    case ShaderUtil.ShaderPropertyType.Range:
                        savedFloats[propName] = material.GetFloat(propName);
                        break;
                }
            }

            // シェーダーを変更
            Undo.RecordObject(material, "Convert Shader");
            material.shader = _targetShader;

            // プロパティマッピング
            var mapping = new Dictionary<string, string>
            {
                { "_MainTex", "_BaseMap" },
                { "_Color", "_BaseColor" },
            };

            // プロパティを復元
            foreach (var kvp in savedTextures)
            {
                string targetProp = mapping.GetValueOrDefault(kvp.Key, kvp.Key);
                if (material.HasProperty(targetProp))
                {
                    material.SetTexture(targetProp, kvp.Value);
                }
            }

            foreach (var kvp in savedColors)
            {
                string targetProp = mapping.GetValueOrDefault(kvp.Key, kvp.Key);
                if (material.HasProperty(targetProp))
                {
                    material.SetColor(targetProp, kvp.Value);
                }
            }

            foreach (var kvp in savedFloats)
            {
                string targetProp = mapping.GetValueOrDefault(kvp.Key, kvp.Key);
                if (material.HasProperty(targetProp))
                {
                    material.SetFloat(targetProp, kvp.Value);
                }
            }

            // ノイズテクスチャを設定
            if (_noiseTexture != null && material.HasProperty("_NoiseMap"))
            {
                material.SetTexture("_NoiseMap", _noiseTexture);
            }

            // エッジカラーを設定
            if (material.HasProperty("_EdgeColor"))
            {
                material.SetColor("_EdgeColor", new Color(3f, 1.5f, 0.3f, 1f));
            }

            EditorUtility.SetDirty(material);
            return true;
        }
    }
}
