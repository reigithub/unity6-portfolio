using UnityEngine;
using UnityEditor;

public static class SwitchEnemyMaterialShader
{
    private static readonly string[] EnemyMaterialPaths =
    {
        "Assets/StoreAssets/DungeonMason/RPG Monster DUO PBR Polyart/Materials/PolyartDefault.mat",
        "Assets/StoreAssets/DungeonMason/RPGMonsterPartnersPBRPolyart/Materials/PolyartDefault.mat",
        "Assets/StoreAssets/DungeonMason/FourEvilDragonsHP/Materials/DragonNightmare/DarkBlueHP.mat",
        "Assets/StoreAssets/DungeonMason/FourEvilDragonsHP/Materials/DragonSoulEater/GreyHP.mat",
        "Assets/StoreAssets/DungeonMason/FourEvilDragonsHP/Materials/DragonTerrorBringer/PurpleHP.mat",
        "Assets/StoreAssets/DungeonMason/FourEvilDragonsHP/Materials/DragonUsurper/RedHP.mat",
        "Assets/StoreAssets/DungeonMason/PartyMonsterDuo/Material/DefaultPolyart.mat",
        "Assets/StoreAssets/DungeonMason/PartyMonsterDuo/Material/DefaultPBR01.mat",
        "Assets/StoreAssets/DungeonMason/PartyMonsterDuo/Material/DefaultPBR02.mat",
        "Assets/StoreAssets/DungeonMason/Monster Minion Survivor PBR Polyart/Material/PA_Default.mat",
    };

    [MenuItem("Tools/Switch Enemy Material to CharacterUnlit")]
    public static void Execute()
    {
        var unlitShader = Shader.Find("Game/Character/CharacterUnlit");
        if (unlitShader == null)
        {
            Debug.LogError("[SwitchShader] Shader not found: Game/Character/CharacterUnlit");
            return;
        }

        int count = 0;
        foreach (string path in EnemyMaterialPaths)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Debug.LogWarning("[SwitchShader] Material not found: " + path);
                continue;
            }

            if (mat.shader == unlitShader)
            {
                Debug.Log("[SwitchShader] Already CharacterUnlit: " + path);
                count++;
                continue;
            }

            // テクスチャ参照を保持（シェーダー変更で失われる可能性があるため）
            var mainTex = mat.GetTexture("_MainTex");
            var baseMap = mat.GetTexture("_BaseMap");
            var baseColor = mat.HasColor("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
            var color = mat.HasColor("_Color") ? mat.GetColor("_Color") : Color.white;

            string prevShader = mat.shader.name;
            mat.shader = unlitShader;

            // テクスチャを復元
            var tex = baseMap != null ? baseMap : mainTex;
            if (tex != null) mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", baseMap != null ? baseColor : color);

            EditorUtility.SetDirty(mat);
            count++;
            Debug.Log("[SwitchShader] " + prevShader + " -> CharacterUnlit: " + path);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[SwitchShader] Done. " + count + " materials processed.");
    }
}
