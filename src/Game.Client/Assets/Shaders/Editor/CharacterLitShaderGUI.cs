using UnityEditor;
using UnityEngine;

namespace Game.Editor.Shaders
{
    /// <summary>
    /// CharacterLitシェーダー用のカスタムShaderGUI
    /// テクスチャの有無に基づいてキーワードを自動設定
    /// </summary>
    public class CharacterLitShaderGUI : ShaderGUI
    {
        private MaterialProperty _baseMap;
        private MaterialProperty _baseColor;
        private MaterialProperty _metallicGlossMap;
        private MaterialProperty _metallic;
        private MaterialProperty _smoothness;
        private MaterialProperty _bumpMap;
        private MaterialProperty _bumpScale;
        private MaterialProperty _occlusionMap;
        private MaterialProperty _occlusionStrength;
        private MaterialProperty _emissionMap;
        private MaterialProperty _emissionColor;
        private MaterialProperty _flashColor;
        private MaterialProperty _flashAmount;
        private MaterialProperty _dissolveAmount;
        private MaterialProperty _noiseMap;
        private MaterialProperty _noiseScale;
        private MaterialProperty _edgeColor;
        private MaterialProperty _edgeWidth;
        private MaterialProperty _dissolveDirection;
        private MaterialProperty _directionalInfluence;
        private MaterialProperty _cull;
        private MaterialProperty _receiveShadows;

        private bool _showBaseSection = true;
        private bool _showPBRSection = true;
        private bool _showEffectsSection = true;
        private bool _showDissolveSection = true;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            FindProperties(properties);

            Material material = materialEditor.target as Material;

            EditorGUILayout.Space(5);

            // Base Section
            _showBaseSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showBaseSection, "Base");
            if (_showBaseSection)
            {
                EditorGUI.indentLevel++;
                materialEditor.TexturePropertySingleLine(
                    new GUIContent("Albedo", "Base texture and color"),
                    _baseMap, _baseColor);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // PBR Section
            _showPBRSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showPBRSection, "PBR Properties");
            if (_showPBRSection)
            {
                EditorGUI.indentLevel++;

                // Metallic/Smoothness
                materialEditor.TexturePropertySingleLine(
                    new GUIContent("Metallic Map", "Metallic (R) Smoothness (A)"),
                    _metallicGlossMap);

                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(_metallic, "Metallic");
                materialEditor.ShaderProperty(_smoothness, "Smoothness");
                EditorGUI.indentLevel--;

                EditorGUILayout.Space(5);

                // Normal
                materialEditor.TexturePropertySingleLine(
                    new GUIContent("Normal Map"),
                    _bumpMap, _bumpScale);

                // Occlusion
                materialEditor.TexturePropertySingleLine(
                    new GUIContent("Occlusion"),
                    _occlusionMap, _occlusionStrength);

                EditorGUILayout.Space(5);

                // Emission
                materialEditor.TexturePropertySingleLine(
                    new GUIContent("Emission"),
                    _emissionMap, _emissionColor);

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // Effects Section
            _showEffectsSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showEffectsSection, "Hit Flash");
            if (_showEffectsSection)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(_flashColor, "Flash Color");
                materialEditor.ShaderProperty(_flashAmount, "Flash Amount");
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // Dissolve Section
            _showDissolveSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showDissolveSection, "Dissolve");
            if (_showDissolveSection)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(_dissolveAmount, "Dissolve Amount");
                materialEditor.TexturePropertySingleLine(
                    new GUIContent("Noise Map"),
                    _noiseMap, _noiseScale);
                materialEditor.ShaderProperty(_edgeColor, "Edge Color");
                materialEditor.ShaderProperty(_edgeWidth, "Edge Width");
                materialEditor.ShaderProperty(_dissolveDirection, "Direction");
                materialEditor.ShaderProperty(_directionalInfluence, "Directional Influence");
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(10);

            // Rendering
            EditorGUILayout.LabelField("Rendering", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            materialEditor.ShaderProperty(_cull, "Cull Mode");
            materialEditor.ShaderProperty(_receiveShadows, "Receive Shadows");
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(10);

            // Update keywords based on texture presence
            SetKeywords(material);

            // Render queue
            materialEditor.RenderQueueField();
        }

        private void FindProperties(MaterialProperty[] properties)
        {
            _baseMap = FindProperty("_BaseMap", properties, false);
            _baseColor = FindProperty("_BaseColor", properties, false);
            _metallicGlossMap = FindProperty("_MetallicGlossMap", properties, false);
            _metallic = FindProperty("_Metallic", properties, false);
            _smoothness = FindProperty("_Smoothness", properties, false);
            _bumpMap = FindProperty("_BumpMap", properties, false);
            _bumpScale = FindProperty("_BumpScale", properties, false);
            _occlusionMap = FindProperty("_OcclusionMap", properties, false);
            _occlusionStrength = FindProperty("_OcclusionStrength", properties, false);
            _emissionMap = FindProperty("_EmissionMap", properties, false);
            _emissionColor = FindProperty("_EmissionColor", properties, false);
            _flashColor = FindProperty("_FlashColor", properties, false);
            _flashAmount = FindProperty("_FlashAmount", properties, false);
            _dissolveAmount = FindProperty("_DissolveAmount", properties, false);
            _noiseMap = FindProperty("_NoiseMap", properties, false);
            _noiseScale = FindProperty("_NoiseScale", properties, false);
            _edgeColor = FindProperty("_EdgeColor", properties, false);
            _edgeWidth = FindProperty("_EdgeWidth", properties, false);
            _dissolveDirection = FindProperty("_DissolveDirection", properties, false);
            _directionalInfluence = FindProperty("_DirectionalInfluence", properties, false);
            _cull = FindProperty("_Cull", properties, false);
            _receiveShadows = FindProperty("_ReceiveShadows", properties, false);
        }

        private void SetKeywords(Material material)
        {
            // Metallic map
            bool hasMetallicMap = _metallicGlossMap?.textureValue != null;
            SetKeyword(material, "_METALLICSPECGLOSSMAP", hasMetallicMap);

            // Normal map
            bool hasNormalMap = _bumpMap?.textureValue != null;
            SetKeyword(material, "_NORMALMAP", hasNormalMap);

            // Occlusion map
            bool hasOcclusionMap = _occlusionMap?.textureValue != null;
            SetKeyword(material, "_OCCLUSIONMAP", hasOcclusionMap);

            // Emission
            bool hasEmission = _emissionMap?.textureValue != null ||
                              (_emissionColor?.colorValue ?? Color.black) != Color.black;
            SetKeyword(material, "_EMISSION", hasEmission);
        }

        private void SetKeyword(Material material, string keyword, bool enable)
        {
            if (enable)
            {
                material.EnableKeyword(keyword);
            }
            else
            {
                material.DisableKeyword(keyword);
            }
        }
    }
}
