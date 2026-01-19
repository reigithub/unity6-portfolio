using Game.MVP.Survivor.Enemy;
using Game.MVP.Survivor.Item;
using Game.MVP.Survivor.Player;
using Game.MVP.Survivor.Scenes;
using Game.MVP.Survivor.Weapon;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Editor.Survivor
{
    /// <summary>
    /// Survivorゲーム用プレハブ自動生成エディタ
    /// </summary>
    public static class SurvivorPrefabCreator
    {
        private const string BasePath = "Assets/ProjectAssets/Survivor";
        private const string PrefabsPath = BasePath + "/Prefabs";
        private const string PlayerPath = PrefabsPath + "/Player";
        private const string EnemyPath = PrefabsPath + "/Enemy";
        private const string WeaponPath = PrefabsPath + "/Weapon";
        private const string ItemPath = PrefabsPath + "/Item";

        [MenuItem("Tools/Survivor/Create All Prefabs")]
        public static void CreateAllPrefabs()
        {
            SetupTagsAndLayers(); // タグを先に設定
            EnsureDirectories();
            CreatePlayerPrefab();
            CreateEnemyPrefabs();
            CreateProjectilePrefab();
            CreateExperienceOrbPrefab();
            AssetDatabase.Refresh();
            Debug.Log("[SurvivorPrefabCreator] All prefabs created successfully!");
        }

        [MenuItem("Tools/Survivor/Create Player Prefab")]
        public static void CreatePlayerPrefab()
        {
            EnsureDirectories();

            var playerGo = new GameObject("SurvivorPlayer");

            // CharacterController
            var cc = playerGo.AddComponent<CharacterController>();
            cc.center = new Vector3(0, 0.9f, 0);
            cc.radius = 0.3f;
            cc.height = 1.8f;

            // SurvivorPlayerController
            var controller = playerGo.AddComponent<SurvivorPlayerController>();

            // Model placeholder
            var modelGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            modelGo.name = "Model";
            modelGo.transform.SetParent(playerGo.transform);
            modelGo.transform.localPosition = new Vector3(0, 1f, 0);
            modelGo.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

            // Colliderを削除（CharacterControllerがあるため）
            Object.DestroyImmediate(modelGo.GetComponent<CapsuleCollider>());

            // マテリアル設定
            var renderer = modelGo.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = Color.cyan;
            renderer.sharedMaterial = mat;
            AssetDatabase.CreateAsset(mat, $"{PlayerPath}/SurvivorPlayerMaterial.mat");

            // Tag設定
            playerGo.tag = "Player";

            // プレハブ保存
            var prefabPath = $"{PlayerPath}/SurvivorPlayer.prefab";
            PrefabUtility.SaveAsPrefabAsset(playerGo, prefabPath);
            Object.DestroyImmediate(playerGo);

            Debug.Log($"[SurvivorPrefabCreator] Created: {prefabPath}");
        }

        [MenuItem("Tools/Survivor/Create Enemy Prefabs")]
        public static void CreateEnemyPrefabs()
        {
            AddTag("Enemy"); // タグを確保
            EnsureDirectories();

            // 基本の敵タイプを作成
            CreateEnemyPrefab("SurvivorEnemy_Slime", Color.green, 0.8f);
            CreateEnemyPrefab("SurvivorEnemy_Skeleton", Color.white, 1.0f);
            CreateEnemyPrefab("SurvivorEnemy_Demon", Color.red, 1.2f);
        }

        private static void CreateEnemyPrefab(string name, Color color, float scale)
        {
            var enemyGo = new GameObject(name);

            // NavMeshAgent
            var agent = enemyGo.AddComponent<NavMeshAgent>();
            agent.speed = 3f;
            agent.angularSpeed = 360f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 0.5f;
            agent.radius = 0.3f;
            agent.height = 1.5f;

            // Collider (トリガー)
            var collider = enemyGo.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0, 0.75f, 0);
            collider.radius = 0.4f;
            collider.height = 1.5f;
            collider.isTrigger = true;

            // SurvivorEnemyController
            var controller = enemyGo.AddComponent<SurvivorEnemyController>();

            // Model placeholder
            var modelGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            modelGo.name = "Model";
            modelGo.transform.SetParent(enemyGo.transform);
            modelGo.transform.localPosition = new Vector3(0, 0.5f * scale, 0);
            modelGo.transform.localScale = Vector3.one * scale;

            // Colliderを削除
            Object.DestroyImmediate(modelGo.GetComponent<SphereCollider>());

            // マテリアル設定
            var renderer = modelGo.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            renderer.sharedMaterial = mat;
            AssetDatabase.CreateAsset(mat, $"{EnemyPath}/{name}Material.mat");

            // Tag設定
            enemyGo.tag = "Enemy";

            // プレハブ保存
            var prefabPath = $"{EnemyPath}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(enemyGo, prefabPath);
            Object.DestroyImmediate(enemyGo);

            Debug.Log($"[SurvivorPrefabCreator] Created: {prefabPath}");
        }

        [MenuItem("Tools/Survivor/Create Projectile Prefab")]
        public static void CreateProjectilePrefab()
        {
            AddTag("Projectile"); // タグを確保
            EnsureDirectories();

            var projectileGo = new GameObject("SurvivorProjectile");

            // Collider (トリガー)
            var collider = projectileGo.AddComponent<SphereCollider>();
            collider.radius = 0.2f;
            collider.isTrigger = true;

            // Rigidbody (キネマティック)
            var rb = projectileGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // SurvivorProjectile
            var projectile = projectileGo.AddComponent<SurvivorProjectile>();

            // TrailRenderer
            var trail = projectileGo.AddComponent<TrailRenderer>();
            trail.time = 0.3f;
            trail.startWidth = 0.2f;
            trail.endWidth = 0.05f;
            var trailMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(trailMat, $"{WeaponPath}/ProjectileTrailMaterial.mat");
            trail.sharedMaterial = trailMat;
            trail.startColor = Color.yellow;
            trail.endColor = new Color(1f, 1f, 0f, 0f);

            // Visual (Sphere)
            var visualGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visualGo.name = "Visual";
            visualGo.transform.SetParent(projectileGo.transform);
            visualGo.transform.localPosition = Vector3.zero;
            visualGo.transform.localScale = Vector3.one * 0.3f;
            Object.DestroyImmediate(visualGo.GetComponent<SphereCollider>());

            // マテリアル設定
            var renderer = visualGo.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = Color.yellow;
            mat.SetFloat("_Smoothness", 0.8f);
            renderer.sharedMaterial = mat;
            AssetDatabase.CreateAsset(mat, $"{WeaponPath}/ProjectileMaterial.mat");

            // Tag設定
            projectileGo.tag = "Projectile";

            // プレハブ保存
            var prefabPath = $"{WeaponPath}/SurvivorProjectile.prefab";
            PrefabUtility.SaveAsPrefabAsset(projectileGo, prefabPath);
            Object.DestroyImmediate(projectileGo);

            Debug.Log($"[SurvivorPrefabCreator] Created: {prefabPath}");
        }

        [MenuItem("Tools/Survivor/Create Experience Orb Prefab")]
        public static void CreateExperienceOrbPrefab()
        {
            AddTag("ExperienceOrb"); // タグを確保
            EnsureDirectories();

            var orbGo = new GameObject("ExperienceOrb");

            // Collider (トリガー)
            var collider = orbGo.AddComponent<SphereCollider>();
            collider.radius = 0.3f;
            collider.isTrigger = true;

            // ExperienceOrb
            var orb = orbGo.AddComponent<SurvivorExperienceOrb>();

            // Visual (Sphere with emission)
            var visualGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visualGo.name = "Visual";
            visualGo.transform.SetParent(orbGo.transform);
            visualGo.transform.localPosition = Vector3.zero;
            visualGo.transform.localScale = Vector3.one * 0.4f;
            Object.DestroyImmediate(visualGo.GetComponent<SphereCollider>());

            // マテリアル設定（発光）
            var renderer = visualGo.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.2f, 0.8f, 1f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.2f, 0.8f, 1f) * 2f);
            renderer.sharedMaterial = mat;
            AssetDatabase.CreateAsset(mat, $"{ItemPath}/ExperienceOrbMaterial.mat");

            // Tag設定
            orbGo.tag = "ExperienceOrb";

            // プレハブ保存
            var prefabPath = $"{ItemPath}/ExperienceOrb.prefab";
            PrefabUtility.SaveAsPrefabAsset(orbGo, prefabPath);
            Object.DestroyImmediate(orbGo);

            Debug.Log($"[SurvivorPrefabCreator] Created: {prefabPath}");
        }

        private static void EnsureDirectories()
        {
            CreateDirectoryIfNotExists(BasePath);
            CreateDirectoryIfNotExists(PrefabsPath);
            CreateDirectoryIfNotExists(PlayerPath);
            CreateDirectoryIfNotExists(EnemyPath);
            CreateDirectoryIfNotExists(WeaponPath);
            CreateDirectoryIfNotExists(ItemPath);
        }

        [MenuItem("Tools/Survivor/Create Stage Scene Prefab")]
        public static void CreateStageScenePrefab()
        {
            EnsureDirectories();
            CreateDirectoryIfNotExists(BasePath + "/Scenes");

            var sceneGo = new GameObject("SurvivorStageScene");

            // SurvivorStageSceneComponent
            var sceneComponent = sceneGo.AddComponent<SurvivorStageSceneComponent>();

            // Canvas (HUD)
            var canvasGo = CreateCanvas("HUDCanvas", sceneGo.transform);

            // HP Slider
            var hpSliderGo = CreateSlider("HPSlider", canvasGo.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -30), new Vector2(200, 20));

            // HP Text
            var hpTextGo = CreateText("HPText", canvasGo.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(230, -30), "100/100");

            // EXP Slider
            var expSliderGo = CreateSlider("EXPSlider", canvasGo.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -60), new Vector2(200, 15));

            // Level Text
            var levelTextGo = CreateText("LevelText", canvasGo.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(230, -60), "Lv.1");

            // Time Text
            var timeTextGo = CreateText("TimeText", canvasGo.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), "00:00");

            // Kills Text
            var killsTextGo = CreateText("KillsText", canvasGo.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-100, -30), "Kills: 0");

            // Wave Text
            var waveTextGo = CreateText("WaveText", canvasGo.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-100, -60), "Wave 1");

            // Pause Button
            var pauseButtonGo = CreateButton("PauseButton", canvasGo.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-50, -100), "||");

            // Game Over Panel
            var gameOverPanel = CreatePanel("GameOverPanel", canvasGo.transform, "GAME OVER");
            gameOverPanel.SetActive(false);

            // Victory Panel
            var victoryPanel = CreatePanel("VictoryPanel", canvasGo.transform, "VICTORY!");
            victoryPanel.SetActive(false);

            // Player
            var playerGo = new GameObject("PlayerController");
            playerGo.transform.SetParent(sceneGo.transform);
            playerGo.AddComponent<SurvivorPlayerController>();
            playerGo.AddComponent<CharacterController>();
            playerGo.tag = "Player";

            // Enemy Spawner
            var spawnerGo = new GameObject("EnemySpawner");
            spawnerGo.transform.SetParent(sceneGo.transform);
            spawnerGo.AddComponent<SurvivorEnemySpawner>();

            // Weapon Manager
            var weaponGo = new GameObject("WeaponManager");
            weaponGo.transform.SetParent(sceneGo.transform);
            weaponGo.AddComponent<SurvivorWeaponManager>();

            // Experience Orb Spawner
            var orbSpawnerGo = new GameObject("ExperienceOrbSpawner");
            orbSpawnerGo.transform.SetParent(sceneGo.transform);
            orbSpawnerGo.AddComponent<SurvivorExperienceOrbSpawner>();

            // プレハブ保存
            var prefabPath = $"{BasePath}/Scenes/SurvivorStageScene.prefab";
            PrefabUtility.SaveAsPrefabAsset(sceneGo, prefabPath);
            Object.DestroyImmediate(sceneGo);

            Debug.Log($"[SurvivorPrefabCreator] Created: {prefabPath}");
        }

        [MenuItem("Tools/Survivor/Setup Tags and Layers")]
        public static void SetupTagsAndLayers()
        {
            // Tags
            AddTag("Enemy");
            AddTag("ExperienceOrb");
            AddTag("Projectile");

            Debug.Log("[SurvivorPrefabCreator] Tags setup completed!");
        }

        private static void AddTag(string tagName)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var tagsProp = tagManager.FindProperty("tags");

            // 既存タグをチェック
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == tagName)
                {
                    return; // 既に存在
                }
            }

            // タグを追加
            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tagName;
            tagManager.ApplyModifiedProperties();

            Debug.Log($"[SurvivorPrefabCreator] Added tag: {tagName}");
        }

        private static GameObject CreateCanvas(string name, Transform parent)
        {
            var canvasGo = new GameObject(name);
            canvasGo.transform.SetParent(parent);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            return canvasGo;
        }

        private static GameObject CreateSlider(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var sliderGo = new GameObject(name);
            sliderGo.transform.SetParent(parent);

            var rectTransform = sliderGo.AddComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = new Vector2(0, 1);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;

            // Background
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(sliderGo.transform);
            var bgRect = bgGo.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            var bgImage = bgGo.AddComponent<UnityEngine.UI.Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f);

            // Fill Area
            var fillAreaGo = new GameObject("Fill Area");
            fillAreaGo.transform.SetParent(sliderGo.transform);
            var fillAreaRect = fillAreaGo.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.sizeDelta = Vector2.zero;

            // Fill
            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(fillAreaGo.transform);
            var fillRect = fillGo.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
            var fillImage = fillGo.AddComponent<UnityEngine.UI.Image>();
            fillImage.color = Color.green;

            var slider = sliderGo.AddComponent<UnityEngine.UI.Slider>();
            slider.fillRect = fillRect;
            slider.interactable = false;

            return sliderGo;
        }

        private static GameObject CreateText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, string text)
        {
            var textGo = new GameObject(name);
            textGo.transform.SetParent(parent);

            var rectTransform = textGo.AddComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = new Vector2(0.5f, 1);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = new Vector2(200, 30);

            var tmp = textGo.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 24;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;

            return textGo;
        }

        private static GameObject CreateButton(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, string text)
        {
            var buttonGo = new GameObject(name);
            buttonGo.transform.SetParent(parent);

            var rectTransform = buttonGo.AddComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = new Vector2(1, 1);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = new Vector2(60, 60);

            var image = buttonGo.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.3f, 0.3f, 0.3f);

            buttonGo.AddComponent<UnityEngine.UI.Button>();

            // Text
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(buttonGo.transform);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            var tmp = textGo.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 24;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;

            return buttonGo;
        }

        private static GameObject CreatePanel(string name, Transform parent, string text)
        {
            var panelGo = new GameObject(name);
            panelGo.transform.SetParent(parent);

            var rectTransform = panelGo.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;

            var image = panelGo.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0, 0, 0, 0.7f);

            // Text
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(panelGo.transform);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = new Vector2(400, 100);

            var tmp = textGo.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 48;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;

            return panelGo;
        }

        private static void CreateDirectoryIfNotExists(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parentPath = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
                var folderName = System.IO.Path.GetFileName(path);
                AssetDatabase.CreateFolder(parentPath, folderName);
            }
        }
    }
}