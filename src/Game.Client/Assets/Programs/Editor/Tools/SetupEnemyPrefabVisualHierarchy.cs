using UnityEngine;
using UnityEditor;

public static class SetupEnemyPrefabVisualHierarchy
{
    [MenuItem("Tools/Setup Prefab Visual Hierarchy")]
    public static void Execute()
    {
        int processed = 0;

        // Enemy prefabs
        string enemyRoot = "Assets/ProjectAssets/Survivor/Prefabs/Enemy";
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { enemyRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponent<Game.MVP.Survivor.Enemy.SurvivorEnemyController>() == null)
                continue;

            using (var scope = new PrefabUtility.EditPrefabContentsScope(path))
            {
                var root = scope.prefabContentsRoot;
                SetupEnemyVisual(root);
                processed++;
                Debug.Log("[SetupVisual] " + path);
            }
        }

        // Player prefab
        string playerPath = "Assets/ProjectAssets/Survivor/Prefabs/Network/SurvivorFusionPlayer.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(playerPath) != null)
        {
            using (var scope = new PrefabUtility.EditPrefabContentsScope(playerPath))
            {
                SetupPlayerVisual(scope.prefabContentsRoot);
                processed++;
                Debug.Log("[SetupVisual] " + playerPath);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[SetupVisual] Done. " + processed + " prefabs.");
    }

    private static void SetupEnemyVisual(GameObject root)
    {
        var ctrl = root.GetComponent<Game.MVP.Survivor.Enemy.SurvivorEnemyController>();
        var anim = root.GetComponent<Animator>();
        var vfx = root.GetComponent<Game.MVP.Survivor.Enemy.EnemyVisualEffectController>();
        var renderers = root.GetComponentsInChildren<Renderer>();

        // 既存の Visual 子を削除（Presenter 廃止に伴い不要）
        var existingVisual = root.transform.Find("Visual");
        if (existingVisual != null)
            Object.DestroyImmediate(existingVisual.gameObject);

        // Controller の SerializeField 設定
        var cSo = new SerializedObject(ctrl);
        var animProp = cSo.FindProperty("_animator");
        if (animProp != null) animProp.objectReferenceValue = anim;
        var renderersProp = cSo.FindProperty("_renderers");
        if (renderersProp != null)
        {
            renderersProp.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++)
                renderersProp.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
        }

        var vfxProp = cSo.FindProperty("_visualEffectController");
        if (vfxProp != null) vfxProp.objectReferenceValue = vfx;
        cSo.ApplyModifiedPropertiesWithoutUndo();

        // EcsEnemyProxy をプレハブ Root に事前配置（disabled 状態）
        // CreateProxy 時の AddComponent + Awake GetComponentInChildren を排除する
        var proxy = root.GetComponent<Game.MVP.Survivor.ECS.EcsEnemyProxy>();
        if (proxy == null) proxy = root.AddComponent<Game.MVP.Survivor.ECS.EcsEnemyProxy>();
        proxy.enabled = false;

        // Controller の _collider SerializedProperty から Collider 参照を取得して proxy に設定
        var ctrlSo2 = new SerializedObject(ctrl);
        var colliderRef = ctrlSo2.FindProperty("_collider").objectReferenceValue as Collider;

        var proxySo = new SerializedObject(proxy);
        proxySo.FindProperty("_collider").objectReferenceValue = colliderRef;
        proxySo.FindProperty("_animator").objectReferenceValue = anim;
        proxySo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetupPlayerVisual(GameObject root)
    {
        var ctrl = root.GetComponent<Game.MVP.Survivor.Player.SurvivorPlayerController>();

        Transform vt = root.transform.Find("Visual");
        if (vt == null)
        {
            var go = new GameObject("Visual");
            go.transform.SetParent(root.transform, false);
            go.SetActive(false);
            vt = go.transform;
        }
        else
        {
            vt.gameObject.SetActive(false);
        }

        if (vt.GetComponent<Game.Shared.Components.VisualRoot>() == null)
            vt.gameObject.AddComponent<Game.Shared.Components.VisualRoot>();

        var p = vt.GetComponent<Game.MVP.Survivor.Player.SurvivorPlayerPresenter>();
        if (p == null) p = vt.gameObject.AddComponent<Game.MVP.Survivor.Player.SurvivorPlayerPresenter>();

        var pSo = new SerializedObject(p);
        pSo.FindProperty("_controller").objectReferenceValue = ctrl;
        pSo.ApplyModifiedPropertiesWithoutUndo();

        // SurvivorPlayerController._visual に Visual GameObject を設定
        var ctrlSo = new SerializedObject(ctrl);
        ctrlSo.FindProperty("_visual").objectReferenceValue = vt.gameObject;
        ctrlSo.ApplyModifiedPropertiesWithoutUndo();
    }
}
