using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Game.MVP.Survivor.UI;

namespace Game.Editor.Survivor
{
    /// <summary>
    /// SurvivorCountdownDialogプレハブのセットアップツール
    /// </summary>
    public static class SurvivorCountdownDialogSetup
    {
        [MenuItem("Tools/Survivor/Setup Countdown Dialog Prefab")]
        public static void SetupPrefab()
        {
            const string prefabPath = "Assets/ProjectAssets/Survivor/UI/SurvivorCountdownDialog.prefab";
            const string uxmlPath = "Assets/Programs/Runtime/MVP/Survivor/UI/SurvivorCountdownDialog.uxml";

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[SurvivorCountdownDialogSetup] Prefab not found: {prefabPath}");
                return;
            }

            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            if (uxml == null)
            {
                Debug.LogError($"[SurvivorCountdownDialogSetup] UXML not found: {uxmlPath}");
                return;
            }

            // プレハブを編集モードで開く
            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                var uiDocument = prefabRoot.GetComponent<UIDocument>();
                var dialogComponent = prefabRoot.GetComponent<SurvivorCountdownDialogComponent>();

                if (uiDocument == null)
                {
                    uiDocument = prefabRoot.AddComponent<UIDocument>();
                }

                if (dialogComponent == null)
                {
                    dialogComponent = prefabRoot.AddComponent<SurvivorCountdownDialogComponent>();
                }

                // UIDocumentにUXMLを設定
                uiDocument.visualTreeAsset = uxml;

                // DialogComponentにUIDocument参照を設定
                var so = new SerializedObject(dialogComponent);
                var uiDocProp = so.FindProperty("_uiDocument");
                uiDocProp.objectReferenceValue = uiDocument;
                so.ApplyModifiedPropertiesWithoutUndo();

                // プレハブを保存
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);

                Debug.Log($"[SurvivorCountdownDialogSetup] Prefab setup complete: {prefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.Refresh();
        }
    }
}
