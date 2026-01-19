using Game.Shared;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Survivor
{
    /// <summary>
    /// Survivorプレイヤープレハブの設定を行うエディタツール
    /// Rigidbody + RaycastCheckerベースの設定（SDUnityChanPlayerControllerと同じアプローチ）
    /// </summary>
    public static class SurvivorPlayerPrefabSetup
    {
        private const string PrefabPath = "Assets/ProjectAssets/Survivor/Prefabs/Player/SDUnityChan.prefab";

        [MenuItem("Project/Survivor/Setup Player Prefab (Rigidbody)")]
        public static void SetupPlayerPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Prefab not found at {PrefabPath}");
                return;
            }

            var prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);

            try
            {
                bool modified = false;

                // CharacterControllerを削除（Rigidbodyベースに移行）
                var characterController = prefabRoot.GetComponent<CharacterController>();
                if (characterController != null)
                {
                    Object.DestroyImmediate(characterController);
                    Debug.Log("CharacterController removed (switching to Rigidbody-based movement)");
                    modified = true;
                }

                // Animatorの設定（Apply Root Motionを無効化）
                var animator = prefabRoot.GetComponent<Animator>();
                if (animator != null && animator.applyRootMotion)
                {
                    animator.applyRootMotion = false;
                    Debug.Log("Animator: Apply Root Motion disabled");
                    modified = true;
                }

                // Rigidbodyの設定
                var rigidbody = prefabRoot.GetComponent<Rigidbody>();
                if (rigidbody == null)
                {
                    rigidbody = prefabRoot.AddComponent<Rigidbody>();
                    Debug.Log("Rigidbody added");
                    modified = true;
                }

                // Rigidbodyを物理シミュレーション用に設定
                if (rigidbody.isKinematic)
                {
                    rigidbody.isKinematic = false;
                    modified = true;
                }
                if (!rigidbody.useGravity)
                {
                    rigidbody.useGravity = true;
                    modified = true;
                }
                rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                Debug.Log($"Rigidbody configured: isKinematic=false, useGravity=true, FreezeRotation, Interpolate");

                // CapsuleColliderの設定
                var capsuleCollider = prefabRoot.GetComponent<CapsuleCollider>();
                if (capsuleCollider == null)
                {
                    capsuleCollider = prefabRoot.AddComponent<CapsuleCollider>();
                    Debug.Log("CapsuleCollider added");
                    modified = true;
                }

                // CapsuleColliderを有効化して設定
                if (!capsuleCollider.enabled)
                {
                    capsuleCollider.enabled = true;
                    modified = true;
                }
                // SDユニティちゃん用のサイズ設定
                capsuleCollider.center = new Vector3(0f, 0.4f, 0f);
                capsuleCollider.radius = 0.2f;
                capsuleCollider.height = 0.8f;
                Debug.Log($"CapsuleCollider configured: center=(0, 0.4, 0), radius=0.2, height=0.8");

                // RaycastCheckerの設定（接地判定用）
                var raycastChecker = prefabRoot.GetComponent<RaycastChecker>();
                if (raycastChecker == null)
                {
                    raycastChecker = prefabRoot.AddComponent<RaycastChecker>();
                    Debug.Log("RaycastChecker added");
                    modified = true;
                }

                // RaycastCheckerの設定をSerializedObjectで更新
                var raycastSo = new SerializedObject(raycastChecker);
                var posOffsetProp = raycastSo.FindProperty("_positionOffset");
                var directionProp = raycastSo.FindProperty("_direction");
                var distanceProp = raycastSo.FindProperty("_distance");

                if (posOffsetProp != null)
                    posOffsetProp.vector3Value = new Vector3(0f, 0.1f, 0f);
                if (directionProp != null)
                    directionProp.vector3Value = Vector3.down;
                if (distanceProp != null)
                    distanceProp.floatValue = 0.2f;

                raycastSo.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("RaycastChecker configured: offset=(0, 0.1, 0), direction=down, distance=0.2");

                if (modified)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                    Debug.Log($"Prefab saved: {PrefabPath}");
                }

                Debug.Log("=== Player prefab setup complete (Rigidbody-based) ===");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.Refresh();
        }

        [MenuItem("Project/Survivor/List Player Components")]
        public static void ListPlayerComponents()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Prefab not found at {PrefabPath}");
                return;
            }

            Debug.Log($"=== Components on {PrefabPath} ===");
            var components = prefab.GetComponents<Component>();
            foreach (var component in components)
            {
                Debug.Log($"  - {component.GetType().Name}");
            }
        }
    }
}
