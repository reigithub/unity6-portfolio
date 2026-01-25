using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Shaders
{
    /// <summary>
    /// マテリアルのシェーダーを一括変換するエディタユーティリティ
    /// </summary>
    public class MaterialShaderConverter : EditorWindow
    {
        private Shader _targetShader;
        private DefaultAsset _searchFolder;
        private List<Material> _foundMaterials = new();
        private Vector2 _scrollPosition;
        private bool _includeSubfolders = true;

        // プロパティマッピング（URP/Lit → CharacterLit）
        private static readonly Dictionary<string, string> PropertyMapping = new()
        {
            { "_MainTex", "_BaseMap" },
            { "_Color", "_BaseColor" },
        };

        [MenuItem("Tools/Game/Material Shader Converter")]
        public static void ShowWindow()
        {
            var window = GetWindow<MaterialShaderConverter>("Shader Converter");
            window.minSize = new Vector2(400, 500);
        }

        private void OnEnable()
        {
            _targetShader = Shader.Find("Game/Character/CharacterLit");
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("マテリアルシェーダー一括変換", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox(
                "指定フォルダ内のマテリアルを検索し、シェーダーを一括変換します。\n" +
                "テクスチャ設定は自動的に引き継がれます。",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // 設定
            _searchFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                "検索フォルダ",
                _searchFolder,
                typeof(DefaultAsset),
                false);

            _includeSubfolders = EditorGUILayout.Toggle("サブフォルダも含む", _includeSubfolders);

            _targetShader = (Shader)EditorGUILayout.ObjectField(
                "変換先シェーダー",
                _targetShader,
                typeof(Shader),
                false);

            EditorGUILayout.Space(10);

            // 検索ボタン
            using (new EditorGUI.DisabledGroupScope(_searchFolder == null))
            {
                if (GUILayout.Button("マテリアルを検索", GUILayout.Height(30)))
                {
                    SearchMaterials();
                }
            }

            EditorGUILayout.Space(10);

            // 結果表示
            if (_foundMaterials.Count > 0)
            {
                EditorGUILayout.LabelField($"検出されたマテリアル: {_foundMaterials.Count}件", EditorStyles.boldLabel);

                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(250));

                foreach (var mat in _foundMaterials)
                {
                    if (mat == null) continue;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.ObjectField(mat, typeof(Material), false);
                        EditorGUILayout.LabelField(mat.shader.name, GUILayout.Width(200));
                    }
                }

                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space(10);

                // 変換ボタン
                using (new EditorGUI.DisabledGroupScope(_targetShader == null))
                {
                    GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
                    if (GUILayout.Button("すべて変換", GUILayout.Height(35)))
                    {
                        ConvertAllMaterials();
                    }
                    GUI.backgroundColor = Color.white;
                }
            }

            EditorGUILayout.Space(10);

            // クイック変換セクション
            EditorGUILayout.LabelField("クイック変換", EditorStyles.boldLabel);

            if (GUILayout.Button("Enemy フォルダのマテリアルを変換"))
            {
                QuickConvertEnemyMaterials();
            }

            if (GUILayout.Button("選択中のマテリアルを変換"))
            {
                ConvertSelectedMaterials();
            }
        }

        private void SearchMaterials()
        {
            _foundMaterials.Clear();

            if (_searchFolder == null) return;

            string folderPath = AssetDatabase.GetAssetPath(_searchFolder);
            string searchOption = _includeSubfolders ? "t:Material" : "t:Material";

            string[] guids = AssetDatabase.FindAssets(searchOption, new[] { folderPath });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (!_includeSubfolders)
                {
                    // サブフォルダを除外
                    string relativePath = path.Substring(folderPath.Length + 1);
                    if (relativePath.Contains("/")) continue;
                }

                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null && material.shader != _targetShader)
                {
                    _foundMaterials.Add(material);
                }
            }

            Debug.Log($"[MaterialShaderConverter] {_foundMaterials.Count}件のマテリアルを検出しました");
        }

        private void ConvertAllMaterials()
        {
            if (_targetShader == null)
            {
                EditorUtility.DisplayDialog("エラー", "変換先シェーダーを指定してください", "OK");
                return;
            }

            int converted = 0;
            Undo.RecordObjects(_foundMaterials.ToArray(), "Convert Material Shaders");

            foreach (var material in _foundMaterials)
            {
                if (ConvertMaterial(material))
                {
                    converted++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[MaterialShaderConverter] {converted}件のマテリアルを変換しました");
            EditorUtility.DisplayDialog("完了", $"{converted}件のマテリアルを変換しました", "OK");
        }

        private bool ConvertMaterial(Material material)
        {
            if (material == null || _targetShader == null) return false;

            // 現在のテクスチャを保存
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
            material.shader = _targetShader;

            // プロパティを復元（マッピングを適用）
            foreach (var kvp in savedTextures)
            {
                string targetProp = PropertyMapping.GetValueOrDefault(kvp.Key, kvp.Key);
                if (material.HasProperty(targetProp))
                {
                    material.SetTexture(targetProp, kvp.Value);
                }
            }

            foreach (var kvp in savedColors)
            {
                string targetProp = PropertyMapping.GetValueOrDefault(kvp.Key, kvp.Key);
                if (material.HasProperty(targetProp))
                {
                    material.SetColor(targetProp, kvp.Value);
                }
            }

            foreach (var kvp in savedFloats)
            {
                string targetProp = PropertyMapping.GetValueOrDefault(kvp.Key, kvp.Key);
                if (material.HasProperty(targetProp))
                {
                    material.SetFloat(targetProp, kvp.Value);
                }
            }

            // ノイズテクスチャを自動設定（ディゾルブ用）
            if (material.HasProperty("_NoiseMap") && material.GetTexture("_NoiseMap") == null)
            {
                var noiseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/StoreAssets/JMO Assets/Cartoon FX Remaster/CFXR Assets/Graphics/cfxr noise clouds big.png");
                if (noiseTexture != null)
                {
                    material.SetTexture("_NoiseMap", noiseTexture);
                }
            }

            EditorUtility.SetDirty(material);
            return true;
        }

        private void QuickConvertEnemyMaterials()
        {
            _targetShader = Shader.Find("Game/Character/CharacterLit");
            if (_targetShader == null)
            {
                EditorUtility.DisplayDialog("エラー", "Game/Character/CharacterLit シェーダーが見つかりません", "OK");
                return;
            }

            // 敵プレハブで使用されているマテリアルを検索
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab",
                new[] { "Assets/ProjectAssets/Survivor/Prefabs/Enemy" });

            var materialsToConvert = new HashSet<Material>();

            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var renderers = prefab.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in renderers)
                {
                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mat != null && mat.shader != _targetShader)
                        {
                            materialsToConvert.Add(mat);
                        }
                    }
                }
            }

            if (materialsToConvert.Count == 0)
            {
                EditorUtility.DisplayDialog("結果", "変換対象のマテリアルが見つかりませんでした", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("確認",
                $"{materialsToConvert.Count}件のマテリアルを変換しますか？\n\n" +
                "この操作はUndo可能です。",
                "変換", "キャンセル"))
            {
                return;
            }

            int converted = 0;
            var materialArray = new Material[materialsToConvert.Count];
            materialsToConvert.CopyTo(materialArray);
            Undo.RecordObjects(materialArray, "Convert Enemy Material Shaders");

            foreach (var material in materialsToConvert)
            {
                if (ConvertMaterial(material))
                {
                    converted++;
                    Debug.Log($"[MaterialShaderConverter] 変換: {material.name}");
                }
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("完了", $"{converted}件のマテリアルを変換しました", "OK");
        }

        private void ConvertSelectedMaterials()
        {
            _targetShader = Shader.Find("Game/Character/CharacterLit");
            if (_targetShader == null)
            {
                EditorUtility.DisplayDialog("エラー", "Game/Character/CharacterLit シェーダーが見つかりません", "OK");
                return;
            }

            var selectedMaterials = Selection.GetFiltered<Material>(SelectionMode.Assets);
            if (selectedMaterials.Length == 0)
            {
                EditorUtility.DisplayDialog("エラー", "マテリアルを選択してください", "OK");
                return;
            }

            Undo.RecordObjects(selectedMaterials, "Convert Selected Material Shaders");

            int converted = 0;
            foreach (var material in selectedMaterials)
            {
                if (ConvertMaterial(material))
                {
                    converted++;
                }
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("完了", $"{converted}件のマテリアルを変換しました", "OK");
        }
    }
}
