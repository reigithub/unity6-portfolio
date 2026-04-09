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

        Transform vt = root.transform.Find("Visual");
        if (vt == null)
        {
            vt = new GameObject("Visual").transform;
            vt.SetParent(root.transform, false);
        }

        var p = vt.GetComponent<Game.MVP.Survivor.Enemy.SurvivorEnemyPresenter>();
        if (p == null) p = vt.gameObject.AddComponent<Game.MVP.Survivor.Enemy.SurvivorEnemyPresenter>();

        var pSo = new SerializedObject(p);
        pSo.FindProperty("_controller").objectReferenceValue = ctrl;
        pSo.FindProperty("_animator").objectReferenceValue = anim;
        pSo.FindProperty("_visualEffectController").objectReferenceValue = vfx;
        pSo.ApplyModifiedPropertiesWithoutUndo();

        var cSo = new SerializedObject(ctrl);
        cSo.FindProperty("_visual").objectReferenceValue = vt.gameObject;
        cSo.ApplyModifiedPropertiesWithoutUndo();
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
