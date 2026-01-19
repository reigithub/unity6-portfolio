using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor.Survivor
{
    /// <summary>
    /// シーン内のオブジェクトにコライダーを一括設定するエディタツール
    /// - 地面系: MeshCollider以外があれば置き換え（キャラクター浮遊防止）
    /// - 構造物: コライダーがなければMeshColliderを追加（貫通防止）
    /// </summary>
    public static class SceneColliderSetup
    {
        // 地面系と判定するキーワード（小文字で比較）
        private static readonly string[] GroundKeywords =
        {
            "street", "road", "floor", "ground", "pavement", "plane", "terrain",
            "path", "sidewalk", "walkway", "tile", "plate"
        };

        // 構造物と判定するキーワード（小文字で比較）
        private static readonly string[] StructureKeywords =
        {
            "house", "building", "wall", "fence", "door", "window", "roof",
            "chimney", "gate", "bench", "lamp", "post", "sign", "box",
            "scaffold", "ladder", "pipe", "gutter", "barrel", "crate",
            "rock", "stone", "grave", "cross", "tree", "prop"
        };

        // 除外するキーワード（コライダーをつけない）
        private static readonly string[] ExcludeKeywords =
        {
            "decal", "particle", "effect", "light", "camera", "audio",
            "trigger", "spawn", "start", "point", "marker", "lod1", "lod2"
        };

        [MenuItem("Project/Survivor/Setup Scene Colliders")]
        public static void SetupAllColliders()
        {
            int groundFixed = 0;
            int structureFixed = 0;
            int skipped = 0;

            // シーン内の全MeshRendererを持つオブジェクトを取得
            var allMeshRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);

            foreach (var meshRenderer in allMeshRenderers)
            {
                var go = meshRenderer.gameObject;
                var nameLower = go.name.ToLower();
                var parentNameLower = go.transform.parent != null ? go.transform.parent.name.ToLower() : "";

                // 除外対象かチェック
                if (ShouldExclude(nameLower))
                {
                    continue;
                }

                // MeshFilterがあるか確認
                var meshFilter = go.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                // 地面系かチェック
                if (IsGroundObject(nameLower, parentNameLower))
                {
                    if (FixGroundCollider(go, meshFilter))
                    {
                        groundFixed++;
                    }
                }
                // 構造物かチェック
                else if (IsStructureObject(nameLower, parentNameLower))
                {
                    if (AddStructureCollider(go, meshFilter))
                    {
                        structureFixed++;
                    }
                }
                else
                {
                    skipped++;
                }
            }

            Debug.Log($"=== Scene Collider Setup Complete ===");
            Debug.Log($"Ground objects fixed: {groundFixed}");
            Debug.Log($"Structure colliders added: {structureFixed}");
            Debug.Log($"Skipped (unrecognized): {skipped}");

            if (groundFixed > 0 || structureFixed > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
        }

        [MenuItem("Project/Survivor/Setup Ground Colliders Only")]
        public static void SetupGroundCollidersOnly()
        {
            int fixedCount = 0;
            var allMeshRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);

            foreach (var meshRenderer in allMeshRenderers)
            {
                var go = meshRenderer.gameObject;
                var nameLower = go.name.ToLower();
                var parentNameLower = go.transform.parent != null ? go.transform.parent.name.ToLower() : "";

                if (ShouldExclude(nameLower)) continue;

                var meshFilter = go.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null) continue;

                if (IsGroundObject(nameLower, parentNameLower))
                {
                    if (FixGroundCollider(go, meshFilter))
                    {
                        fixedCount++;
                    }
                }
            }

            Debug.Log($"=== Ground Collider Setup Complete ===");
            Debug.Log($"Fixed: {fixedCount} objects");

            if (fixedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
        }

        [MenuItem("Project/Survivor/Setup Structure Colliders Only")]
        public static void SetupStructureCollidersOnly()
        {
            int addedCount = 0;
            var allMeshRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);

            foreach (var meshRenderer in allMeshRenderers)
            {
                var go = meshRenderer.gameObject;
                var nameLower = go.name.ToLower();
                var parentNameLower = go.transform.parent != null ? go.transform.parent.name.ToLower() : "";

                if (ShouldExclude(nameLower)) continue;

                var meshFilter = go.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null) continue;

                if (IsStructureObject(nameLower, parentNameLower))
                {
                    if (AddStructureCollider(go, meshFilter))
                    {
                        addedCount++;
                    }
                }
            }

            Debug.Log($"=== Structure Collider Setup Complete ===");
            Debug.Log($"Added: {addedCount} colliders");

            if (addedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
        }

        [MenuItem("Project/Survivor/Apply Slippery Material to Structures")]
        public static void ApplySlipperyMaterialToStructures()
        {
            int appliedCount = 0;
            var allMeshRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            var slipperyMaterial = GetOrCreateSlipperyMaterial();

            foreach (var meshRenderer in allMeshRenderers)
            {
                var go = meshRenderer.gameObject;
                var nameLower = go.name.ToLower();
                var parentNameLower = go.transform.parent != null ? go.transform.parent.name.ToLower() : "";

                if (ShouldExclude(nameLower)) continue;

                // 構造物かチェック
                if (!IsStructureObject(nameLower, parentNameLower)) continue;

                // コライダーがあり、まだマテリアルが設定されていない場合
                var collider = go.GetComponent<Collider>();
                if (collider != null && collider.sharedMaterial != slipperyMaterial)
                {
                    Undo.RecordObject(collider, "Apply Slippery Material");
                    collider.sharedMaterial = slipperyMaterial;
                    appliedCount++;
                }
            }

            Debug.Log($"=== Apply Slippery Material Complete ===");
            Debug.Log($"Applied to {appliedCount} structure colliders");

            if (appliedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
        }

        [MenuItem("Project/Survivor/List Objects Without Colliders")]
        public static void ListObjectsWithoutColliders()
        {
            Debug.Log("=== Objects with MeshRenderer but no Collider ===");

            var allMeshRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            var noColliderObjects = new List<string>();

            foreach (var meshRenderer in allMeshRenderers)
            {
                var go = meshRenderer.gameObject;
                if (go.GetComponent<Collider>() == null)
                {
                    noColliderObjects.Add($"{go.name} ({GetHierarchyPath(go)})");
                }
            }

            // グループ化して表示
            var grouped = noColliderObjects
                .GroupBy(s => s.Split('/').LastOrDefault()?.Split('(').FirstOrDefault()?.Trim() ?? "Unknown")
                .OrderByDescending(g => g.Count());

            foreach (var group in grouped.Take(30))
            {
                Debug.Log($"{group.Key}: {group.Count()} objects");
            }

            Debug.Log($"\nTotal: {noColliderObjects.Count} objects without colliders");
        }

        /// <summary>
        /// 地面オブジェクトのコライダーを修正（MeshCollider以外→MeshCollider）
        /// </summary>
        private static bool FixGroundCollider(GameObject go, MeshFilter meshFilter)
        {
            var existingCollider = go.GetComponent<Collider>();

            // 既にMeshColliderがあればスキップ
            if (existingCollider is MeshCollider)
            {
                return false;
            }

            // 既存コライダーの設定を保存して削除
            bool isTrigger = false;
            PhysicsMaterial material = null;

            if (existingCollider != null)
            {
                isTrigger = existingCollider.isTrigger;
                material = existingCollider.sharedMaterial;
                Undo.DestroyObjectImmediate(existingCollider);
            }

            // MeshColliderを追加
            var meshCollider = Undo.AddComponent<MeshCollider>(go);
            meshCollider.sharedMesh = meshFilter.sharedMesh;
            meshCollider.isTrigger = isTrigger;
            meshCollider.sharedMaterial = material;
            meshCollider.convex = false;

            Debug.Log($"Ground fixed: {go.name}");
            return true;
        }

        /// <summary>
        /// 構造物にコライダーを追加（コライダーがない場合のみ）
        /// 低摩擦のPhysicsMaterialを適用してプレイヤーが引っかかりにくくする
        /// </summary>
        private static bool AddStructureCollider(GameObject go, MeshFilter meshFilter)
        {
            // 既にコライダーがあればスキップ
            if (go.GetComponent<Collider>() != null)
            {
                return false;
            }

            // MeshColliderを追加
            var meshCollider = Undo.AddComponent<MeshCollider>(go);
            meshCollider.sharedMesh = meshFilter.sharedMesh;
            meshCollider.convex = false;

            // 低摩擦マテリアルを適用
            meshCollider.sharedMaterial = GetOrCreateSlipperyMaterial();

            Debug.Log($"Structure collider added: {go.name}");
            return true;
        }

        private const string SlipperyMaterialPath = "Assets/ProjectAssets/Survivor/PhysicsMaterials/SlipperyStructure.physicMaterial";

        /// <summary>
        /// 低摩擦PhysicsMaterialを取得または作成
        /// </summary>
        private static PhysicsMaterial GetOrCreateSlipperyMaterial()
        {
            // 既存のマテリアルを探す
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(SlipperyMaterialPath);
            if (material != null)
            {
                return material;
            }

            // フォルダを作成（フォワードスラッシュで統一）
            var folderPath = "Assets/ProjectAssets/Survivor/PhysicsMaterials";
            var parts = folderPath.Split('/');
            var currentPath = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var nextPath = currentPath + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, parts[i]);
                    Debug.Log($"Created folder: {nextPath}");
                }
                currentPath = nextPath;
            }

            // 新しいマテリアルを作成（ObjectFactoryを使用）
            material = ObjectFactory.CreateInstance<PhysicsMaterial>();
            material.name = "SlipperyStructure";
            material.dynamicFriction = 0.1f;
            material.staticFriction = 0.1f;
            material.bounciness = 0f;
            material.frictionCombine = PhysicsMaterialCombine.Minimum;
            material.bounceCombine = PhysicsMaterialCombine.Minimum;

            AssetDatabase.CreateAsset(material, SlipperyMaterialPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Created slippery physics material: {SlipperyMaterialPath}");
            return material;
        }

        private static bool IsGroundObject(string nameLower, string parentNameLower)
        {
            return GroundKeywords.Any(keyword =>
                nameLower.Contains(keyword) || parentNameLower.Contains(keyword));
        }

        private static bool IsStructureObject(string nameLower, string parentNameLower)
        {
            return StructureKeywords.Any(keyword =>
                nameLower.Contains(keyword) || parentNameLower.Contains(keyword));
        }

        private static bool ShouldExclude(string nameLower)
        {
            return ExcludeKeywords.Any(keyword => nameLower.Contains(keyword));
        }

        private static string GetHierarchyPath(GameObject go)
        {
            var path = go.name;
            var parent = go.transform.parent;
            int depth = 0;
            while (parent != null && depth < 3)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
                depth++;
            }
            return path;
        }
    }
}
