using UnityEditor;
using UnityEngine;

namespace Game.Editor.Shaders
{
    /// <summary>
    /// ToonLitシェーダーのカスタムInspector
    /// マテリアルプロパティを整理して表示
    /// </summary>
    public class ToonLitShaderGUI : ShaderGUI
    {
        // Foldout states (EditorPrefs for persistence)
        private const string PrefKeyBase = "ToonLitShaderGUI_";

        private bool _showBaseSettings = true;
        private bool _showToonSettings = true;
        private bool _showRimSettings = true;
        private bool _showOutlineSettings = true;
        private bool _showEmissionSettings = false;
        private bool _showRenderingSettings = false;

        // Property cache
        private MaterialProperty _baseMap;
        private MaterialProperty _baseColor;
        private MaterialProperty _shadeColor;
        private MaterialProperty _shadeThreshold;
        private MaterialProperty _shadeSmoothness;
        private MaterialProperty _shadowAttenuation;
        private MaterialProperty _rampMap;
        private MaterialProperty _rimColor;
        private MaterialProperty _rimPower;
        private MaterialProperty _rimThreshold;
        private MaterialProperty _rimSmoothness;
        private MaterialProperty _outlineColor;
        private MaterialProperty _outlineWidth;
        private MaterialProperty _emissionColor;
        private MaterialProperty _cull;
        private MaterialProperty _zWrite;

        private bool _isFirstTime = true;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            // キャッシュプロパティ
            CacheProperties(properties);

            // 初回のみ設定を読み込み
            if (_isFirstTime)
            {
                LoadFoldoutStates();
                _isFirstTime = false;
            }

            EditorGUILayout.Space(5);

            // ヘッダー
            DrawHeader("Game/ToonLit Shader");

            EditorGUILayout.Space(10);

            // Base Settings
            _showBaseSettings = DrawFoldout("Base", _showBaseSettings);
            if (_showBaseSettings)
            {
                EditorGUI.indentLevel++;
                DrawTextureProperty(materialEditor, _baseMap, _baseColor, "Base Map & Color");
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }

            // Toon Shading Settings
            _showToonSettings = DrawFoldout("Toon Shading", _showToonSettings);
            if (_showToonSettings)
            {
                EditorGUI.indentLevel++;
                DrawProperty(materialEditor, _shadeColor, "Shade Color");
                DrawSliderProperty(materialEditor, _shadeThreshold, "Shade Threshold");
                DrawSliderProperty(materialEditor, _shadeSmoothness, "Shade Smoothness");
                DrawSliderProperty(materialEditor, _shadowAttenuation, "Shadow Attenuation");

                EditorGUILayout.Space(3);
                DrawTexturePropertySingleLine(materialEditor, _rampMap, "Ramp Map (Optional)");

                EditorGUILayout.Space(3);
                EditorGUILayout.HelpBox(
                    "Shade Threshold: Controls the light/shadow transition point.\n" +
                    "Shade Smoothness: Controls the softness of the transition.",
                    MessageType.None);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }

            // Rim Light Settings
            _showRimSettings = DrawFoldout("Rim Light", _showRimSettings);
            if (_showRimSettings)
            {
                EditorGUI.indentLevel++;
                DrawProperty(materialEditor, _rimColor, "Rim Color (HDR)");
                DrawSliderProperty(materialEditor, _rimPower, "Rim Power");
                DrawSliderProperty(materialEditor, _rimThreshold, "Rim Threshold");
                DrawSliderProperty(materialEditor, _rimSmoothness, "Rim Smoothness");

                EditorGUILayout.Space(3);
                EditorGUILayout.HelpBox(
                    "Rim Power: Higher values create a thinner rim effect.",
                    MessageType.None);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }

            // Outline Settings
            _showOutlineSettings = DrawFoldout("Outline", _showOutlineSettings);
            if (_showOutlineSettings)
            {
                EditorGUI.indentLevel++;
                DrawProperty(materialEditor, _outlineColor, "Outline Color");
                DrawSliderProperty(materialEditor, _outlineWidth, "Outline Width");

                EditorGUILayout.Space(3);
                EditorGUILayout.HelpBox(
                    "Outline is rendered using a separate pass with front-face culling.",
                    MessageType.None);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }

            // Emission Settings
            _showEmissionSettings = DrawFoldout("Emission", _showEmissionSettings);
            if (_showEmissionSettings)
            {
                EditorGUI.indentLevel++;
                DrawProperty(materialEditor, _emissionColor, "Emission Color (HDR)");
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }

            // Rendering Settings
            _showRenderingSettings = DrawFoldout("Rendering", _showRenderingSettings);
            if (_showRenderingSettings)
            {
                EditorGUI.indentLevel++;
                DrawProperty(materialEditor, _cull, "Cull Mode");
                DrawProperty(materialEditor, _zWrite, "ZWrite");

                EditorGUILayout.Space(5);

                // Render Queue
                materialEditor.RenderQueueField();

                EditorGUILayout.Space(3);

                // GPU Instancing
                materialEditor.EnableInstancingField();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Preset Buttons
            DrawPresetButtons(materialEditor);

            // Save foldout states
            SaveFoldoutStates();
        }

        private void CacheProperties(MaterialProperty[] properties)
        {
            _baseMap = FindProperty("_BaseMap", properties, false);
            _baseColor = FindProperty("_BaseColor", properties, false);
            _shadeColor = FindProperty("_ShadeColor", properties, false);
            _shadeThreshold = FindProperty("_ShadeThreshold", properties, false);
            _shadeSmoothness = FindProperty("_ShadeSmoothness", properties, false);
            _shadowAttenuation = FindProperty("_ShadowAttenuation", properties, false);
            _rampMap = FindProperty("_RampMap", properties, false);
            _rimColor = FindProperty("_RimColor", properties, false);
            _rimPower = FindProperty("_RimPower", properties, false);
            _rimThreshold = FindProperty("_RimThreshold", properties, false);
            _rimSmoothness = FindProperty("_RimSmoothness", properties, false);
            _outlineColor = FindProperty("_OutlineColor", properties, false);
            _outlineWidth = FindProperty("_OutlineWidth", properties, false);
            _emissionColor = FindProperty("_EmissionColor", properties, false);
            _cull = FindProperty("_Cull", properties, false);
            _zWrite = FindProperty("_ZWrite", properties, false);
        }

        private void DrawHeader(string text)
        {
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
            DrawHorizontalLine();
        }

        private bool DrawFoldout(string title, bool isExpanded)
        {
            var style = new GUIStyle(EditorStyles.foldoutHeader)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12
            };

            EditorGUILayout.BeginHorizontal();
            isExpanded = EditorGUILayout.Foldout(isExpanded, title, true, style);
            EditorGUILayout.EndHorizontal();

            return isExpanded;
        }

        private void DrawProperty(MaterialEditor editor, MaterialProperty property, string label)
        {
            if (property != null)
            {
                editor.ShaderProperty(property, label);
            }
        }

        private void DrawSliderProperty(MaterialEditor editor, MaterialProperty property, string label)
        {
            if (property != null)
            {
                editor.ShaderProperty(property, label);
            }
        }

        private void DrawTextureProperty(MaterialEditor editor, MaterialProperty texture, MaterialProperty color, string label)
        {
            if (texture != null)
            {
                editor.TexturePropertySingleLine(new GUIContent(label), texture, color);
                if (texture.textureValue != null)
                {
                    EditorGUI.indentLevel++;
                    editor.TextureScaleOffsetProperty(texture);
                    EditorGUI.indentLevel--;
                }
            }
        }

        private void DrawTexturePropertySingleLine(MaterialEditor editor, MaterialProperty texture, string label)
        {
            if (texture != null)
            {
                editor.TexturePropertySingleLine(new GUIContent(label), texture);
            }
        }

        private void DrawHorizontalLine()
        {
            EditorGUILayout.Space(2);
            var rect = EditorGUILayout.GetControlRect(false, 1);
            rect.height = 1;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            EditorGUILayout.Space(2);
        }

        private void DrawPresetButtons(MaterialEditor editor)
        {
            EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Anime Style", GUILayout.Height(25)))
            {
                ApplyAnimePreset(editor);
            }

            if (GUILayout.Button("Soft Toon", GUILayout.Height(25)))
            {
                ApplySoftToonPreset(editor);
            }

            if (GUILayout.Button("Hard Edge", GUILayout.Height(25)))
            {
                ApplyHardEdgePreset(editor);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ApplyAnimePreset(MaterialEditor editor)
        {
            var material = editor.target as Material;
            if (material == null) return;

            Undo.RecordObject(material, "Apply Anime Preset");

            material.SetColor("_ShadeColor", new Color(0.6f, 0.5f, 0.7f, 1f));
            material.SetFloat("_ShadeThreshold", 0.5f);
            material.SetFloat("_ShadeSmoothness", 0.02f);
            material.SetColor("_RimColor", new Color(1f, 1f, 1f, 0.3f));
            material.SetFloat("_RimPower", 4f);
            material.SetFloat("_OutlineWidth", 1.5f);
            material.SetColor("_OutlineColor", new Color(0.2f, 0.15f, 0.25f, 1f));

            EditorUtility.SetDirty(material);
        }

        private void ApplySoftToonPreset(MaterialEditor editor)
        {
            var material = editor.target as Material;
            if (material == null) return;

            Undo.RecordObject(material, "Apply Soft Toon Preset");

            material.SetColor("_ShadeColor", new Color(0.7f, 0.7f, 0.75f, 1f));
            material.SetFloat("_ShadeThreshold", 0.4f);
            material.SetFloat("_ShadeSmoothness", 0.2f);
            material.SetColor("_RimColor", new Color(1f, 0.95f, 0.9f, 0.2f));
            material.SetFloat("_RimPower", 3f);
            material.SetFloat("_OutlineWidth", 0.5f);
            material.SetColor("_OutlineColor", new Color(0.3f, 0.3f, 0.3f, 1f));

            EditorUtility.SetDirty(material);
        }

        private void ApplyHardEdgePreset(MaterialEditor editor)
        {
            var material = editor.target as Material;
            if (material == null) return;

            Undo.RecordObject(material, "Apply Hard Edge Preset");

            material.SetColor("_ShadeColor", new Color(0.4f, 0.4f, 0.5f, 1f));
            material.SetFloat("_ShadeThreshold", 0.5f);
            material.SetFloat("_ShadeSmoothness", 0f);
            material.SetColor("_RimColor", new Color(1f, 1f, 1f, 0.5f));
            material.SetFloat("_RimPower", 5f);
            material.SetFloat("_OutlineWidth", 2f);
            material.SetColor("_OutlineColor", Color.black);

            EditorUtility.SetDirty(material);
        }

        private void LoadFoldoutStates()
        {
            _showBaseSettings = EditorPrefs.GetBool(PrefKeyBase + "Base", true);
            _showToonSettings = EditorPrefs.GetBool(PrefKeyBase + "Toon", true);
            _showRimSettings = EditorPrefs.GetBool(PrefKeyBase + "Rim", true);
            _showOutlineSettings = EditorPrefs.GetBool(PrefKeyBase + "Outline", true);
            _showEmissionSettings = EditorPrefs.GetBool(PrefKeyBase + "Emission", false);
            _showRenderingSettings = EditorPrefs.GetBool(PrefKeyBase + "Rendering", false);
        }

        private void SaveFoldoutStates()
        {
            EditorPrefs.SetBool(PrefKeyBase + "Base", _showBaseSettings);
            EditorPrefs.SetBool(PrefKeyBase + "Toon", _showToonSettings);
            EditorPrefs.SetBool(PrefKeyBase + "Rim", _showRimSettings);
            EditorPrefs.SetBool(PrefKeyBase + "Outline", _showOutlineSettings);
            EditorPrefs.SetBool(PrefKeyBase + "Emission", _showEmissionSettings);
            EditorPrefs.SetBool(PrefKeyBase + "Rendering", _showRenderingSettings);
        }
    }
}
