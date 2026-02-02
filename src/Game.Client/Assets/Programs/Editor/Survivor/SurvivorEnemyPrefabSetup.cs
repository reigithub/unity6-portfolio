using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Game.MVP.Survivor.Enemy;

namespace Game.Editor.Survivor
{
    /// <summary>
    /// Survivor用エネミープレハブのコンポーネントセットアップツール
    /// NavMeshAgent, CapsuleCollider, SurvivorEnemyControllerを追加し、参照を設定
    /// 各モンスターに合わせたサイズ設定を適用
    /// </summary>
    public static class SurvivorEnemyPrefabSetup
    {
        private const string ENEMY_PREFABS_PATH = "Assets/ProjectAssets/Survivor/Prefabs/Enemy";

        /// <summary>
        /// モンスターごとのサイズ設定
        /// </summary>
        public struct EnemySizeConfig
        {
            public float NavMeshRadius;
            public float NavMeshHeight;
            public float NavMeshBaseOffset;  // 浮遊モンスター用：地面からのオフセット
            public float ColliderRadius;
            public float ColliderHeight;
            public Vector3 ColliderCenter;
            public float NavMeshSpeed;
            public float StoppingDistance;

            public static EnemySizeConfig Default => new EnemySizeConfig
            {
                NavMeshRadius = 0.5f,
                NavMeshHeight = 2f,
                NavMeshBaseOffset = 0f,
                ColliderRadius = 0.5f,
                ColliderHeight = 1f,
                ColliderCenter = new Vector3(0, 0.5f, 0),
                NavMeshSpeed = 3.5f,
                StoppingDistance = 1.5f
            };
        }

        /// <summary>
        /// 各モンスタープレハブ名に対応するサイズ設定
        /// </summary>
        private static readonly Dictionary<string, EnemySizeConfig> EnemySizeConfigs = new Dictionary<string, EnemySizeConfig>
        {
            // Slime - 小さく丸い、地面を這う
            ["SlimePolyart"] = new EnemySizeConfig
            {
                NavMeshRadius = 0.4f,
                NavMeshHeight = 0.8f,
                NavMeshBaseOffset = 0f,
                ColliderRadius = 0.4f,
                ColliderHeight = 0.6f,
                ColliderCenter = new Vector3(0, 0.3f, 0),
                NavMeshSpeed = 2.5f,
                StoppingDistance = 1.0f
            },

            // TurtleShell - 中型、やや幅広
            ["TurtleShellPolyart"] = new EnemySizeConfig
            {
                NavMeshRadius = 0.5f,
                NavMeshHeight = 1.2f,
                NavMeshBaseOffset = 0f,
                ColliderRadius = 0.5f,
                ColliderHeight = 0.8f,
                ColliderCenter = new Vector3(0, 0.4f, 0),
                NavMeshSpeed = 2.0f,
                StoppingDistance = 1.2f
            },

            // Cactus - 背が高く細い
            ["CactusPA"] = new EnemySizeConfig
            {
                NavMeshRadius = 0.3f,
                NavMeshHeight = 1.8f,
                NavMeshBaseOffset = 0f,
                ColliderRadius = 0.35f,
                ColliderHeight = 1.6f,
                ColliderCenter = new Vector3(0, 0.8f, 0),
                NavMeshSpeed = 2.5f,
                StoppingDistance = 1.5f
            },

            // Mushroom Angry - 小さく丸い
            ["MushroomAngryPA"] = new EnemySizeConfig
            {
                NavMeshRadius = 0.35f,
                NavMeshHeight = 1.0f,
                NavMeshBaseOffset = 0f,
                ColliderRadius = 0.35f,
                ColliderHeight = 0.8f,
                ColliderCenter = new Vector3(0, 0.4f, 0),
                NavMeshSpeed = 3.0f,
                StoppingDistance = 1.0f
            },

            // Mushroom Smile - 小さく丸い
            ["MushroomSmilePA"] = new EnemySizeConfig
            {
                NavMeshRadius = 0.35f,
                NavMeshHeight = 1.0f,
                NavMeshBaseOffset = 0f,
                ColliderRadius = 0.35f,
                ColliderHeight = 0.8f,
                ColliderCenter = new Vector3(0, 0.4f, 0),
                NavMeshSpeed = 3.0f,
                StoppingDistance = 1.0f
            },

            // Beholder - 浮遊する目玉、丸く中型（空中に浮いている）
            ["BeholderPolyart"] = new EnemySizeConfig
            {
                NavMeshRadius = 0.5f,
                NavMeshHeight = 2.5f,           // 浮遊高さを含めた全高
                NavMeshBaseOffset = 1.0f,       // 地面から1m浮遊
                ColliderRadius = 0.5f,
                ColliderHeight = 1.0f,
                ColliderCenter = new Vector3(0, 1.5f, 0),  // 浮遊位置に合わせたコライダー中心
                NavMeshSpeed = 3.5f,
                StoppingDistance = 1.5f
            },

            // ChestMonster - 箱型、中型
            ["ChestMonsterPolyart"] = new EnemySizeConfig
            {
                NavMeshRadius = 0.5f,
                NavMeshHeight = 1.2f,
                NavMeshBaseOffset = 0f,
                ColliderRadius = 0.45f,
                ColliderHeight = 1.0f,
                ColliderCenter = new Vector3(0, 0.5f, 0),
                NavMeshSpeed = 4.0f,
                StoppingDistance = 1.2f
            },

            // Swarm08 - 小さな群れ生物（浮遊）
            ["Swarm08PA"] = new EnemySizeConfig
            {
                NavMeshRadius = 0.25f,
                NavMeshHeight = 1.5f,           // 浮遊高さを含めた全高
                NavMeshBaseOffset = 0.6f,       // 地面から0.6m浮遊
                ColliderRadius = 0.25f,
                ColliderHeight = 0.5f,
                ColliderCenter = new Vector3(0, 0.85f, 0),  // 浮遊位置に合わせたコライダー中心
                NavMeshSpeed = 4.5f,
                StoppingDistance = 0.8f
            },

            // Swarm09 - 小さな群れ生物（浮遊）
            ["Swarm09PA"] = new EnemySizeConfig
            {
                NavMeshRadius = 0.25f,
                NavMeshHeight = 1.5f,           // 浮遊高さを含めた全高
                NavMeshBaseOffset = 0.6f,       // 地面から0.6m浮遊
                ColliderRadius = 0.25f,
                ColliderHeight = 0.5f,
                ColliderCenter = new Vector3(0, 0.85f, 0),  // 浮遊位置に合わせたコライダー中心
                NavMeshSpeed = 4.5f,
                StoppingDistance = 0.8f
            },

            // PartyMonster C01 - 人型、中型
            ["PartyMonster_C01"] = new EnemySizeConfig
            {
                NavMeshRadius = 0.4f,
                NavMeshHeight = 1.8f,
                NavMeshBaseOffset = 0f,
                ColliderRadius = 0.4f,
                ColliderHeight = 1.6f,
                ColliderCenter = new Vector3(0, 0.8f, 0),
                NavMeshSpeed = 3.5f,
                StoppingDistance = 1.5f
            },

            // PartyMonster C02 - 人型、中型
            ["PartyMonster_C02"] = new EnemySizeConfig
            {
                NavMeshRadius = 0.4f,
                NavMeshHeight = 1.8f,
                NavMeshBaseOffset = 0f,
                ColliderRadius = 0.4f,
                ColliderHeight = 1.6f,
                ColliderCenter = new Vector3(0, 0.8f, 0),
                NavMeshSpeed = 3.5f,
                StoppingDistance = 1.5f
            },

            // PartyMonster P01 - 人型、中型
            ["PartyMonster_P01"] = new EnemySizeConfig
            {
                NavMeshRadius = 0.4f,
                NavMeshHeight = 1.8f,
                NavMeshBaseOffset = 0f,
                ColliderRadius = 0.4f,
                ColliderHeight = 1.6f,
                ColliderCenter = new Vector3(0, 0.8f, 0),
                NavMeshSpeed = 3.5f,
                StoppingDistance = 1.5f
            },

            // PartyMonster P02 - 人型、中型
            ["PartyMonster_P02"] = new EnemySizeConfig
            {
                NavMeshRadius = 0.4f,
                NavMeshHeight = 1.8f,
                NavMeshBaseOffset = 0f,
                ColliderRadius = 0.4f,
                ColliderHeight = 1.6f,
                ColliderCenter = new Vector3(0, 0.8f, 0),
                NavMeshSpeed = 3.5f,
                StoppingDistance = 1.5f
            },

            // ==================== Boss Dragons ====================

            // DragonSoulEaterGrey - 大型ドラゴンボス
            ["DragonSoulEaterGrey"] = new EnemySizeConfig
            {
                NavMeshRadius = 1.5f,
                NavMeshHeight = 4.0f,
                NavMeshBaseOffset = 0f,
                ColliderRadius = 1.5f,
                ColliderHeight = 4.0f,
                ColliderCenter = new Vector3(0, 2.0f, 0),
                NavMeshSpeed = 2.0f,
                StoppingDistance = 3.0f
            },

            // DragonNightmareDarkBlue - 大型ドラゴンボス
            ["DragonNightmareDarkBlue"] = new EnemySizeConfig
            {
                NavMeshRadius = 1.5f,
                NavMeshHeight = 4.0f,
                NavMeshBaseOffset = 0f,
                ColliderRadius = 1.5f,
                ColliderHeight = 4.0f,
                ColliderCenter = new Vector3(0, 2.0f, 0),
                NavMeshSpeed = 2.0f,
                StoppingDistance = 3.0f
            },

            // DragonTerrorBringerPurple - 大型ドラゴンボス
            ["DragonTerrorBringerPurple"] = new EnemySizeConfig
            {
                NavMeshRadius = 1.5f,
                NavMeshHeight = 4.0f,
                NavMeshBaseOffset = 0f,
                ColliderRadius = 1.5f,
                ColliderHeight = 4.0f,
                ColliderCenter = new Vector3(0, 2.0f, 0),
                NavMeshSpeed = 2.0f,
                StoppingDistance = 3.0f
            },

            // DragonUsurperRed - 大型ドラゴンボス
            ["DragonUsurperRed"] = new EnemySizeConfig
            {
                NavMeshRadius = 1.5f,
                NavMeshHeight = 4.0f,
                NavMeshBaseOffset = 0f,
                ColliderRadius = 1.5f,
                ColliderHeight = 4.0f,
                ColliderCenter = new Vector3(0, 2.0f, 0),
                NavMeshSpeed = 2.0f,
                StoppingDistance = 3.0f
            }
        };

        [MenuItem("Tools/Survivor/Fix Enemy Tags and Triggers")]
        public static void FixEnemyTagsAndTriggers()
        {
            var prefabPaths = GetAllEnemyPrefabPaths();
            int updatedCount = 0;

            foreach (var prefabPath in prefabPaths)
            {
                var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                if (prefabRoot == null) continue;

                bool modified = false;

                // タグを "Enemy" に設定
                if (!prefabRoot.CompareTag("Enemy"))
                {
                    prefabRoot.tag = "Enemy";
                    modified = true;
                    Debug.Log($"[SurvivorEnemyPrefabSetup] Set tag 'Enemy' on {prefabRoot.name}");
                }

                // コライダーをTriggerに設定
                var collider = prefabRoot.GetComponent<Collider>();
                if (collider != null && !collider.isTrigger)
                {
                    collider.isTrigger = true;
                    modified = true;
                    Debug.Log($"[SurvivorEnemyPrefabSetup] Set isTrigger=true on {prefabRoot.name}");
                }

                if (modified)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    updatedCount++;
                }

                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.Refresh();
            Debug.Log($"[SurvivorEnemyPrefabSetup] Fixed {updatedCount} enemy prefabs (Tag: Enemy, isTrigger: true)");
            EditorUtility.DisplayDialog("Enemy Prefab Fix", $"Fixed {updatedCount} enemy prefabs.\n\nTag: Enemy\nisTrigger: true", "OK");
        }

        [MenuItem("Tools/Survivor/Setup All Enemy Prefab Components")]
        public static void SetupAllEnemyPrefabComponents()
        {
            var prefabPaths = GetAllEnemyPrefabPaths();
            int successCount = 0;
            int skipCount = 0;

            foreach (var prefabPath in prefabPaths)
            {
                var result = SetupEnemyPrefab(prefabPath, forceUpdate: false);
                if (result == SetupResult.Success)
                {
                    successCount++;
                }
                else if (result == SetupResult.Skipped)
                {
                    skipCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SurvivorEnemyPrefabSetup] Setup complete: {successCount} modified, {skipCount} skipped, {prefabPaths.Count} total prefabs.");
        }

        [MenuItem("Tools/Survivor/Update All Enemy Prefab Sizes")]
        public static void UpdateAllEnemyPrefabSizes()
        {
            var prefabPaths = GetAllEnemyPrefabPaths();
            int successCount = 0;

            foreach (var prefabPath in prefabPaths)
            {
                var result = SetupEnemyPrefab(prefabPath, forceUpdate: true);
                if (result == SetupResult.Success)
                {
                    successCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SurvivorEnemyPrefabSetup] Size update complete: {successCount}/{prefabPaths.Count} prefabs updated.");
        }

        [MenuItem("Tools/Survivor/Setup Selected Enemy Prefab")]
        public static void SetupSelectedEnemyPrefab()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                Debug.LogError("[SurvivorEnemyPrefabSetup] Please select a prefab in the Project window.");
                return;
            }

            var path = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab"))
            {
                Debug.LogError("[SurvivorEnemyPrefabSetup] Selected object is not a prefab.");
                return;
            }

            var result = SetupEnemyPrefab(path, forceUpdate: true);
            AssetDatabase.SaveAssets();

            if (result == SetupResult.Success)
            {
                Debug.Log($"[SurvivorEnemyPrefabSetup] Successfully set up: {Path.GetFileName(path)}");
            }
            else if (result == SetupResult.Skipped)
            {
                Debug.Log($"[SurvivorEnemyPrefabSetup] Skipped (already configured): {Path.GetFileName(path)}");
            }
        }

        private static List<string> GetAllEnemyPrefabPaths()
        {
            var paths = new List<string>();
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { ENEMY_PREFABS_PATH });

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".prefab"))
                {
                    paths.Add(path);
                }
            }

            return paths;
        }

        private static EnemySizeConfig GetSizeConfig(string prefabName)
        {
            if (EnemySizeConfigs.TryGetValue(prefabName, out var config))
            {
                return config;
            }
            Debug.LogWarning($"[SurvivorEnemyPrefabSetup] No size config for '{prefabName}', using default.");
            return EnemySizeConfig.Default;
        }

        private enum SetupResult
        {
            Success,
            Skipped,
            Failed
        }

        private static SetupResult SetupEnemyPrefab(string prefabPath, bool forceUpdate)
        {
            // Load prefab as GameObject
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null)
            {
                Debug.LogError($"[SurvivorEnemyPrefabSetup] Failed to load prefab: {prefabPath}");
                return SetupResult.Failed;
            }

            // Check if already has SurvivorEnemyController and skip if not forcing update
            if (!forceUpdate)
            {
                var existingController = prefabAsset.GetComponent<SurvivorEnemyController>();
                if (existingController != null)
                {
                    var so = new SerializedObject(existingController);
                    var navAgentProp = so.FindProperty("_navAgent");
                    var animatorProp = so.FindProperty("_animator");
                    var colliderProp = so.FindProperty("_collider");

                    if (navAgentProp.objectReferenceValue != null &&
                        animatorProp.objectReferenceValue != null &&
                        colliderProp.objectReferenceValue != null)
                    {
                        return SetupResult.Skipped;
                    }
                }
            }

            // Get size config for this prefab
            var prefabName = Path.GetFileNameWithoutExtension(prefabPath);
            var sizeConfig = GetSizeConfig(prefabName);

            // Open prefab for editing
            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError($"[SurvivorEnemyPrefabSetup] Failed to open prefab for editing: {prefabPath}");
                return SetupResult.Failed;
            }

            try
            {
                // Get or add components
                var animator = prefabRoot.GetComponent<Animator>();
                if (animator == null)
                {
                    Debug.LogWarning($"[SurvivorEnemyPrefabSetup] No Animator on prefab: {prefabPath}");
                }

                // NavMeshAgent setup
                var navAgent = prefabRoot.GetComponent<NavMeshAgent>();
                if (navAgent == null)
                {
                    navAgent = prefabRoot.AddComponent<NavMeshAgent>();
                }

                // Apply size config to NavMeshAgent
                navAgent.radius = sizeConfig.NavMeshRadius;
                navAgent.height = sizeConfig.NavMeshHeight;
                navAgent.speed = sizeConfig.NavMeshSpeed;
                navAgent.angularSpeed = 120f;
                navAgent.acceleration = 8f;
                navAgent.stoppingDistance = sizeConfig.StoppingDistance;
                navAgent.baseOffset = sizeConfig.NavMeshBaseOffset;  // 浮遊モンスター用オフセット

                // CapsuleCollider setup
                var capsuleCollider = prefabRoot.GetComponent<CapsuleCollider>();
                if (capsuleCollider == null)
                {
                    capsuleCollider = prefabRoot.AddComponent<CapsuleCollider>();
                }

                // Apply size config to CapsuleCollider
                capsuleCollider.radius = sizeConfig.ColliderRadius;
                capsuleCollider.height = sizeConfig.ColliderHeight;
                capsuleCollider.center = sizeConfig.ColliderCenter;
                capsuleCollider.direction = 1; // Y-axis

                // SurvivorEnemyController setup
                var controller = prefabRoot.GetComponent<SurvivorEnemyController>();
                if (controller == null)
                {
                    controller = prefabRoot.AddComponent<SurvivorEnemyController>();
                }

                // Wire up references using SerializedObject
                var serializedController = new SerializedObject(controller);

                var navAgentProp = serializedController.FindProperty("_navAgent");
                var animatorProp = serializedController.FindProperty("_animator");
                var colliderProp = serializedController.FindProperty("_collider");

                navAgentProp.objectReferenceValue = navAgent;
                animatorProp.objectReferenceValue = animator;
                colliderProp.objectReferenceValue = capsuleCollider;

                serializedController.ApplyModifiedPropertiesWithoutUndo();

                // Save prefab
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);

                Debug.Log($"[SurvivorEnemyPrefabSetup] Configured: {prefabName} (NavMesh r={sizeConfig.NavMeshRadius} h={sizeConfig.NavMeshHeight}, Collider r={sizeConfig.ColliderRadius} h={sizeConfig.ColliderHeight})");
                return SetupResult.Success;
            }
            finally
            {
                // Unload prefab contents
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }
}
