
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Game.Editor.Addressables
{
    /// <summary>
    /// Fusion ネットワークプレハブを SurvivorPrefabs Addressable グループに登録するユーティリティ。
    /// </summary>
    public static class RegisterFusionAddressables
    {
        private const string GroupName = "SurvivorPrefabs";

        private static readonly (string path, string address)[] Prefabs = new[]
        {
            ("Assets/ProjectAssets/Survivor/Prefabs/Network/SurvivorFusionGameState.prefab", "SurvivorFusionGameState"),
            ("Assets/ProjectAssets/Survivor/Prefabs/Network/SurvivorFusionPlayer.prefab", "SurvivorFusionPlayer"),
            ("Assets/ProjectAssets/Survivor/Prefabs/Network/SurvivorFusionEnemyBatchSync.prefab", "SurvivorFusionEnemyBatchSync"),
            ("Assets/ProjectAssets/Survivor/Prefabs/Player/SDUnityChan_Model.prefab", "SDUnityChan_Model"),
        };

        [MenuItem("Tools/Addressables/Register Fusion Network Prefabs")]
        public static void Execute()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[RegisterFusionAddressables] AddressableAssetSettings not found");
                return;
            }

            var group = settings.FindGroup(GroupName);
            if (group == null)
            {
                Debug.LogError($"[RegisterFusionAddressables] Group '{GroupName}' not found");
                return;
            }

            foreach (var (path, address) in Prefabs)
            {
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid))
                {
                    Debug.LogError($"[RegisterFusionAddressables] Asset not found: {path}");
                    continue;
                }

                var entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);
                entry.address = address;
                Debug.Log($"[RegisterFusionAddressables] Registered: {address} → {path}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[RegisterFusionAddressables] Done. Fusion prefabs registered in SurvivorPrefabs group.");
        }
    }
}
#endif
