using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Game.MVP.Survivor.Item;

namespace Game.Editor.Survivor
{
    /// <summary>
    /// Survivor用アイテムプレハブのコンポーネントセットアップツール
    /// SurvivorExperienceOrbをSurvivorItemに置き換え、マスタデータに基づいて設定
    /// </summary>
    public static class SurvivorItemPrefabSetup
    {
        private const string ITEM_PREFABS_PATH = "Assets/StoreAssets/BTM_Assets/BTM_Items_Gems/Prefabs";

        /// <summary>
        /// マスタデータから読み込んだアイテム設定
        /// </summary>
        private struct ItemConfig
        {
            public int Id;
            public string Name;
            public string AssetName;
            public SurvivorItemType ItemType;
            public int EffectValue;
            public int EffectRange;
            public int EffectDuration;
            public int Rarity;
            public float Scale;
        }

        [MenuItem("Tools/Survivor/Setup All Item Prefab Components")]
        public static void SetupAllItemPrefabComponents()
        {
            var itemConfigs = LoadMasterData();
            if (itemConfigs.Count == 0)
            {
                Debug.LogError("[SurvivorItemPrefabSetup] Failed to load master data");
                return;
            }

            var prefabPaths = GetAllItemPrefabPaths();
            int successCount = 0;
            int skipCount = 0;

            foreach (var prefabPath in prefabPaths)
            {
                var prefabName = Path.GetFileNameWithoutExtension(prefabPath);
                var config = itemConfigs.FirstOrDefault(c => c.AssetName == prefabName);

                if (config.AssetName == null)
                {
                    Debug.LogWarning($"[SurvivorItemPrefabSetup] No master data for: {prefabName}");
                    skipCount++;
                    continue;
                }

                var result = SetupItemPrefab(prefabPath, config);
                if (result)
                {
                    successCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SurvivorItemPrefabSetup] Setup complete: {successCount} modified, {skipCount} skipped, {prefabPaths.Count} total prefabs.");
        }

        [MenuItem("Tools/Survivor/Replace ExperienceOrb with SurvivorItem")]
        public static void ReplaceExperienceOrbWithSurvivorItem()
        {
            var itemConfigs = LoadMasterData();
            var prefabPaths = GetAllItemPrefabPaths();
            int replacedCount = 0;

            foreach (var prefabPath in prefabPaths)
            {
                var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefabAsset == null) continue;

                // SurvivorExperienceOrbを持っているか確認
#pragma warning disable 618
                var oldComponent = prefabAsset.GetComponent<SurvivorExperienceOrb>();
#pragma warning restore 618
                if (oldComponent == null) continue;

                var prefabName = Path.GetFileNameWithoutExtension(prefabPath);
                var config = itemConfigs.FirstOrDefault(c => c.AssetName == prefabName);

                // プレハブを編集用に開く
                var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                if (prefabRoot == null) continue;

                try
                {
                    // 古いコンポーネントを削除
#pragma warning disable 618
                    var oldComp = prefabRoot.GetComponent<SurvivorExperienceOrb>();
#pragma warning restore 618
                    if (oldComp != null)
                    {
                        Object.DestroyImmediate(oldComp);
                    }

                    // 新しいコンポーネントを追加
                    var newItem = prefabRoot.AddComponent<SurvivorItem>();

                    // 設定を適用
                    if (config.AssetName != null)
                    {
                        ApplyConfig(newItem, config);
                    }
                    else
                    {
                        // マスタデータがない場合はデフォルト経験値として設定
                        var so = new SerializedObject(newItem);
                        so.FindProperty("_itemType").enumValueIndex = (int)SurvivorItemType.Experience;
                        so.FindProperty("_effectValue").intValue = 5;
                        so.FindProperty("_rarity").intValue = 1;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }

                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    replacedCount++;

                    Debug.Log($"[SurvivorItemPrefabSetup] Replaced: {prefabName}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SurvivorItemPrefabSetup] Replacement complete: {replacedCount} prefabs updated.");
        }

        private static List<string> GetAllItemPrefabPaths()
        {
            var paths = new List<string>();
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { ITEM_PREFABS_PATH });

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

        private static List<ItemConfig> LoadMasterData()
        {
            var configs = new List<ItemConfig>();
            var tsvPath = "Assets/MasterData/Tsv/SurvivorItemMaster.tsv";

            if (!File.Exists(tsvPath))
            {
                Debug.LogError($"[SurvivorItemPrefabSetup] TSV file not found: {tsvPath}");
                return configs;
            }

            var lines = File.ReadAllLines(tsvPath);
            for (int i = 1; i < lines.Length; i++) // Skip header
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var parts = line.Split('\t');
                if (parts.Length < 9) continue;

                configs.Add(new ItemConfig
                {
                    Id = int.Parse(parts[0]),
                    Name = parts[1],
                    AssetName = parts[2],
                    ItemType = (SurvivorItemType)int.Parse(parts[3]),
                    EffectValue = int.Parse(parts[4]),
                    EffectRange = int.Parse(parts[5]),
                    EffectDuration = int.Parse(parts[6]),
                    Rarity = int.Parse(parts[7]),
                    Scale = float.Parse(parts[8])
                });
            }

            return configs;
        }

        private static bool SetupItemPrefab(string prefabPath, ItemConfig config)
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError($"[SurvivorItemPrefabSetup] Failed to open prefab: {prefabPath}");
                return false;
            }

            try
            {
                // SurvivorItemコンポーネントを取得または追加
                var item = prefabRoot.GetComponent<SurvivorItem>();
                if (item == null)
                {
                    // 古いExperienceOrbがあれば削除
#pragma warning disable 618
                    var oldComp = prefabRoot.GetComponent<SurvivorExperienceOrb>();
#pragma warning restore 618
                    if (oldComp != null)
                    {
                        Object.DestroyImmediate(oldComp);
                    }

                    item = prefabRoot.AddComponent<SurvivorItem>();
                }

                // 既存のMeshColliderをトリガーとして設定
                var meshCollider = prefabRoot.GetComponent<MeshCollider>();
                if (meshCollider != null)
                {
                    meshCollider.convex = true;  // トリガーにはConvexが必要
                    meshCollider.isTrigger = true;
                }

                // 不要なSphereColliderがあれば削除
                var sphereCollider = prefabRoot.GetComponent<SphereCollider>();
                if (sphereCollider != null)
                {
                    Object.DestroyImmediate(sphereCollider);
                }

                // 設定を適用
                ApplyConfig(item, config);

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);

                Debug.Log($"[SurvivorItemPrefabSetup] Configured: {config.AssetName} (Type={config.ItemType}, Value={config.EffectValue}, Rarity={config.Rarity})");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void ApplyConfig(SurvivorItem item, ItemConfig config)
        {
            var so = new SerializedObject(item);

            so.FindProperty("_itemId").intValue = config.Id;
            so.FindProperty("_itemType").enumValueIndex = (int)config.ItemType;
            so.FindProperty("_effectValue").intValue = config.EffectValue;
            so.FindProperty("_effectRange").intValue = config.EffectRange;
            so.FindProperty("_effectDuration").intValue = config.EffectDuration;
            so.FindProperty("_rarity").intValue = config.Rarity;
            so.FindProperty("_scale").floatValue = config.Scale > 0f ? config.Scale : 1f;

            // レアリティに応じた吸引距離
            so.FindProperty("_attractDistance").floatValue = 2f + config.Rarity * 1f;

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
