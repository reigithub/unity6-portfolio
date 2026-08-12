#if UNITY_EDITOR
using Game.Horror.WeaponEffect;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace Game.Editor.Horror
{
    /// <summary>
    /// Smokeグレネードの煙フィールド・投擲物プレハブを生成し、投擲物を Addressables（HorrorWeapons）へ登録するビルダー。
    /// - SmokeField.prefab: 正立ルート + <see cref="HorrorSmokeField"/>、子に GroundFog（焼き込み回転 X=-90 を維持、残留座標のみ除去）
    /// - SmokeGrenadeProjectile.prefab: Low Poly Smoke モデル + SphereCollider（描画境界から採寸）+ Rigidbody + <see cref="HorrorSmokeGrenadeProjectile"/>
    /// 再実行可能（既存プレハブは上書き。Addressables 登録は CreateOrMoveEntry で冪等）。
    /// </summary>
    public static class HorrorSmokeGrenadePrefabBuilder
    {
        private const string GroundFogPath = "Assets/StoreAssets/UnityTechnologies/ParticlePack/EffectExamples/Smoke & Steam Effects/Prefabs/GroundFog.prefab";
        private const string SmokeModelPath = "Assets/StoreAssets/Low Poly Weapons VOL.1/Prefabs/Smoke.prefab";
        private const string SmokeFieldPrefabPath = "Assets/ProjectAssets/Horror/WeaponEffect/SmokeField.prefab";
        private const string ProjectilePrefabPath = "Assets/ProjectAssets/Horror/Weapon/SmokeGrenadeProjectile.prefab";
        private const string AddressablesGroupName = "HorrorWeapons";
        private const string ProjectileAddress = "SmokeGrenadeProjectile"; // HorrorWeaponMaster.ProjectileAssetName と一致させる

        [MenuItem("Tools/Horror/Create Smoke Grenade Prefabs")]
        public static void Execute()
        {
            var fieldPrefab = CreateSmokeFieldPrefab();
            if (fieldPrefab == null) return;

            if (CreateProjectilePrefab(fieldPrefab) == null) return;

            RegisterProjectileAddress();
            AssetDatabase.SaveAssets();
            Debug.Log($"[{nameof(HorrorSmokeGrenadePrefabBuilder)}] Done.");
        }

        private static HorrorSmokeField CreateSmokeFieldPrefab()
        {
            var fogSource = AssetDatabase.LoadAssetAtPath<GameObject>(GroundFogPath);
            if (fogSource == null)
            {
                Debug.LogError($"[{nameof(HorrorSmokeGrenadePrefabBuilder)}] GroundFog が見つかりません: {GroundFogPath}");
                return null;
            }

            var root = new GameObject("SmokeField");
            try
            {
                var field = root.AddComponent<HorrorSmokeField>();

                var fog = (GameObject)PrefabUtility.InstantiatePrefab(fogSource);
                fog.transform.SetParent(root.transform, worldPositionStays: false);
                fog.transform.localPosition = Vector3.zero; // デモシーンの残留座標を除去（焼き込み回転はプレハブ値のまま）

                // 子の ParticleSystem 参照をシリアライズ保存する
                var so = new SerializedObject(field);
                so.FindProperty("_particle").objectReferenceValue = fog.GetComponent<ParticleSystem>();
                so.ApplyModifiedPropertiesWithoutUndo();

                var saved = PrefabUtility.SaveAsPrefabAsset(root, SmokeFieldPrefabPath);
                Debug.Log($"[{nameof(HorrorSmokeGrenadePrefabBuilder)}] Created: {SmokeFieldPrefabPath}");
                return saved.GetComponent<HorrorSmokeField>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateProjectilePrefab(HorrorSmokeField fieldPrefab)
        {
            var modelSource = AssetDatabase.LoadAssetAtPath<GameObject>(SmokeModelPath);
            if (modelSource == null)
            {
                Debug.LogError($"[{nameof(HorrorSmokeGrenadePrefabBuilder)}] Smoke モデルが見つかりません: {SmokeModelPath}");
                return null;
            }

            var root = new GameObject("SmokeGrenadeProjectile");
            try
            {
                var rb = root.AddComponent<Rigidbody>();
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // 小型高速物のトンネリング防止
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelSource);
                model.transform.SetParent(root.transform, worldPositionStays: false);
                model.transform.localPosition = Vector3.zero;

                // モデルの描画境界から衝突球を採寸する（モデル差し替えにも追従する）
                var collider = root.AddComponent<SphereCollider>();
                var renderers = root.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers)
                        bounds.Encapsulate(renderer.bounds);

                    collider.center = root.transform.InverseTransformPoint(bounds.center);
                    var extents = bounds.extents;
                    collider.radius = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z));
                }

                var projectile = root.AddComponent<HorrorSmokeGrenadeProjectile>();
                var so = new SerializedObject(projectile);
                so.FindProperty("_smokeFieldPrefab").objectReferenceValue = fieldPrefab;
                so.ApplyModifiedPropertiesWithoutUndo();

                var saved = PrefabUtility.SaveAsPrefabAsset(root, ProjectilePrefabPath);
                Debug.Log($"[{nameof(HorrorSmokeGrenadePrefabBuilder)}] Created: {ProjectilePrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // RegisterHorrorItemAddressables と同じ登録イディオム（FindGroup → CreateOrMoveEntry → address）
        private static void RegisterProjectileAddress()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError($"[{nameof(HorrorSmokeGrenadePrefabBuilder)}] AddressableAssetSettings not found");
                return;
            }

            var group = settings.FindGroup(AddressablesGroupName);
            if (group == null)
            {
                Debug.LogError($"[{nameof(HorrorSmokeGrenadePrefabBuilder)}] Group '{AddressablesGroupName}' not found");
                return;
            }

            var guid = AssetDatabase.AssetPathToGUID(ProjectilePrefabPath);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError($"[{nameof(HorrorSmokeGrenadePrefabBuilder)}] Asset not found: {ProjectilePrefabPath}");
                return;
            }

            var entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);
            entry.address = ProjectileAddress;
            Debug.Log($"[{nameof(HorrorSmokeGrenadePrefabBuilder)}] Registered: {ProjectileAddress} → {ProjectilePrefabPath}");
        }
    }
}
#endif
