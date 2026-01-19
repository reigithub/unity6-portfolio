using UnityEditor;
using UnityEngine;

namespace Game.Editor.Survivor
{
    /// <summary>
    /// マテリアルのシェーダーをURP互換に変換するエディタツール
    /// </summary>
    public static class MaterialShaderFix
    {
        [MenuItem("Project/Survivor/Fix Procedural Fire Materials (URP)")]
        public static void FixProceduralFireMaterials()
        {
            var materialsPath = "Assets/StoreAssets/Hovl Studio/Procedural fire/Materials";
            var shaderGraphName = "Shader Graphs/FireSphere";

            // ShaderGraphを探す
            var shader = Shader.Find(shaderGraphName);
            if (shader == null)
            {
                Debug.LogError($"Shader not found: {shaderGraphName}");
                Debug.Log("Available shaders with 'Fire' in name:");

                // 利用可能なシェーダーをリストアップ
                var allShaders = Resources.FindObjectsOfTypeAll<Shader>();
                foreach (var s in allShaders)
                {
                    if (s.name.ToLower().Contains("fire") || s.name.ToLower().Contains("sphere"))
                    {
                        Debug.Log($"  - {s.name}");
                    }
                }
                return;
            }

            var guids = AssetDatabase.FindAssets("t:Material", new[] { materialsPath });
            int fixedCount = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (material == null) continue;

                if (material.shader != shader)
                {
                    Undo.RecordObject(material, "Fix Material Shader");
                    material.shader = shader;
                    EditorUtility.SetDirty(material);
                    Debug.Log($"Fixed: {material.name} -> {shaderGraphName}");
                    fixedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"=== Material Shader Fix Complete ===");
            Debug.Log($"Fixed {fixedCount} materials");
        }

        [MenuItem("Project/Survivor/List Pink Materials in Scene")]
        public static void ListPinkMaterials()
        {
            var particleSystems = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
            int pinkCount = 0;

            Debug.Log("=== Checking ParticleSystem Materials ===");

            foreach (var ps in particleSystems)
            {
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer == null) continue;

                var material = renderer.sharedMaterial;
                if (material == null)
                {
                    Debug.LogWarning($"{ps.gameObject.name}: Material is null");
                    pinkCount++;
                    continue;
                }

                if (material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                {
                    Debug.LogWarning($"{ps.gameObject.name}: Shader is missing or error ({material.name})");
                    pinkCount++;
                }
            }

            Debug.Log($"Found {pinkCount} ParticleSystems with missing/error shaders");
        }
    }
}
