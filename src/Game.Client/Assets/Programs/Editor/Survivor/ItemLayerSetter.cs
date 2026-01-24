using UnityEditor;
using UnityEngine;

namespace Game.MVP.Survivor.Editor
{
    /// <summary>
    /// アイテムプレハブのLayerを一括設定するエディタースクリプト
    /// </summary>
    public static class ItemLayerSetter
    {
        private const string ItemPrefabFolder = "Assets/StoreAssets/BTM_Assets/BTM_Items_Gems/Prefabs";
        private const string ItemLayerName = "Item";

        [MenuItem("Tools/Survivor/Set Item Layer on Prefabs")]
        public static void SetItemLayerOnPrefabs()
        {
            int itemLayer = LayerMask.NameToLayer(ItemLayerName);
            if (itemLayer == -1)
            {
                Debug.LogError($"[ItemLayerSetter] Layer '{ItemLayerName}' not found. Please add it in Tags and Layers.");
                return;
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { ItemPrefabFolder });
            int modifiedCount = 0;

            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                
                if (prefab == null) continue;

                // プレハブのルートGameObjectのLayerを変更
                if (prefab.layer != itemLayer)
                {
                    // プレハブを編集モードで開く
                    string assetPath = AssetDatabase.GetAssetPath(prefab);
                    using (var editingScope = new PrefabUtility.EditPrefabContentsScope(assetPath))
                    {
                        var root = editingScope.prefabContentsRoot;
                        SetLayerRecursively(root, itemLayer);
                    }
                    modifiedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log($"[ItemLayerSetter] Modified {modifiedCount} prefabs. Set layer to '{ItemLayerName}'.");
        }

        private static void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}
