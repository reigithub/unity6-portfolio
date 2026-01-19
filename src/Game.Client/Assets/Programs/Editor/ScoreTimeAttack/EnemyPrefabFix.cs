using ithappy.Animals_FREE;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Editor.ScoreTimeAttack
{
    /// <summary>
    /// エネミープレハブのNavMeshAgent震え問題を修正
    /// CharacterController/Rigidbodyとの競合を解消
    /// </summary>
    public static class EnemyPrefabFix
    {
        private const string EnemyPrefabFolder = "Assets/ProjectAssets/ScoreTimeAttack/Prefabs/Enemy";

        [MenuItem("Project/ScoreTimeAttack/Fix Enemy Prefabs (NavMeshAgent Jittering)")]
        public static void FixAllEnemyPrefabs()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { EnemyPrefabFolder });
            int fixedCount = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (FixEnemyPrefab(path))
                {
                    fixedCount++;
                }
            }

            Debug.Log($"=== Enemy Prefab Fix Complete ===");
            Debug.Log($"Fixed {fixedCount} prefabs");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Project/ScoreTimeAttack/List Enemy Prefab Components")]
        public static void ListEnemyPrefabComponents()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { EnemyPrefabFolder });

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                Debug.Log($"=== {prefab.name} ===");
                var components = prefab.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp == null) continue;
                    var typeName = comp.GetType().Name;

                    // 特定のコンポーネントの詳細を表示
                    if (comp is NavMeshAgent agent)
                    {
                        Debug.Log($"  - {typeName}: speed={agent.speed}, acceleration={agent.acceleration}, angularSpeed={agent.angularSpeed}");
                    }
                    else if (comp is Rigidbody rb)
                    {
                        Debug.Log($"  - {typeName}: isKinematic={rb.isKinematic}, useGravity={rb.useGravity}");
                    }
                    else if (comp is CharacterController cc)
                    {
                        Debug.Log($"  - {typeName}: (CONFLICT with NavMeshAgent!)");
                    }
                    else
                    {
                        Debug.Log($"  - {typeName}");
                    }
                }
            }
        }

        private static bool FixEnemyPrefab(string prefabPath)
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null) return false;

            bool modified = false;

            try
            {
                // 1. MovePlayerInputを先に削除（CreatureMoverに依存しているため）
                var movePlayerInput = prefabRoot.GetComponent<MovePlayerInput>();
                if (movePlayerInput != null)
                {
                    Object.DestroyImmediate(movePlayerInput);
                    Debug.Log($"{prefabRoot.name}: MovePlayerInput removed");
                    modified = true;
                }

                // 2. CreatureMoverを削除（CharacterControllerに依存しているため）
                var creatureMover = prefabRoot.GetComponent<CreatureMover>();
                if (creatureMover != null)
                {
                    Object.DestroyImmediate(creatureMover);
                    Debug.Log($"{prefabRoot.name}: CreatureMover removed");
                    modified = true;
                }

                // 3. CharacterControllerを削除（NavMeshAgentと競合）
                var characterController = prefabRoot.GetComponent<CharacterController>();
                if (characterController != null)
                {
                    Object.DestroyImmediate(characterController);
                    Debug.Log($"{prefabRoot.name}: CharacterController removed");
                    modified = true;
                }

                // 4. Rigidbodyをkinematicに設定（NavMeshAgentと競合防止）
                var rigidbody = prefabRoot.GetComponent<Rigidbody>();
                if (rigidbody != null)
                {
                    if (!rigidbody.isKinematic)
                    {
                        rigidbody.isKinematic = true;
                        Debug.Log($"{prefabRoot.name}: Rigidbody set to kinematic");
                        modified = true;
                    }
                    if (rigidbody.useGravity)
                    {
                        rigidbody.useGravity = false;
                        Debug.Log($"{prefabRoot.name}: Rigidbody gravity disabled");
                        modified = true;
                    }
                }

                // 5. NavMeshAgentの設定を最適化
                var navMeshAgent = prefabRoot.GetComponent<NavMeshAgent>();
                if (navMeshAgent != null)
                {
                    bool navModified = false;

                    // 加速度を高くして急な方向転換を減らす
                    if (navMeshAgent.acceleration < 50f)
                    {
                        navMeshAgent.acceleration = 100f;
                        navModified = true;
                    }

                    // 回転速度を高くして滑らかに
                    if (navMeshAgent.angularSpeed < 300f)
                    {
                        navMeshAgent.angularSpeed = 500f;
                        navModified = true;
                    }

                    // 停止距離を適切に
                    if (navMeshAgent.stoppingDistance < 0.1f)
                    {
                        navMeshAgent.stoppingDistance = 0.1f;
                        navModified = true;
                    }

                    // 自動ブレーキを無効化（震えの原因になることがある）
                    if (navMeshAgent.autoBraking)
                    {
                        navMeshAgent.autoBraking = false;
                        navModified = true;
                    }

                    if (navModified)
                    {
                        Debug.Log($"{prefabRoot.name}: NavMeshAgent optimized (accel=100, angularSpeed=500, autoBraking=false)");
                        modified = true;
                    }
                }

                if (modified)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                }

                return modified;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }
}
