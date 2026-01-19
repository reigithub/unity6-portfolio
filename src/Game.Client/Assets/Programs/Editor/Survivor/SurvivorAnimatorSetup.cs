using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Editor.Survivor
{
    /// <summary>
    /// Survivor用アニメーターコントローラーのセットアップツール
    /// ステート、トランジション、パラメータを自動設定
    /// </summary>
    public static class SurvivorAnimatorSetup
    {
        // 必要なパラメータ
        private const string PARAM_SPEED = "Speed";
        private const string PARAM_ATTACK = "Attack";
        private const string PARAM_HIT = "Hit";
        private const string PARAM_DEATH = "Death";

        /// <summary>
        /// エネミーアニメーション設定
        /// </summary>
        public class EnemyAnimationConfig
        {
            public string ControllerPath;
            public string IdleClipPath;
            public string RunClipPath;
            public string AttackClipPath;
            public string HitClipPath;
            public string DeathClipPath;
        }

        [MenuItem("Tools/Survivor/Add Missing Animator Parameters")]
        public static void AddMissingAnimatorParameters()
        {
            var controllerPaths = GetAllEnemyAnimatorControllerPaths();
            int updatedCount = 0;

            foreach (var path in controllerPaths)
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (controller == null) continue;

                bool modified = false;

                // Speed (Float) - for locomotion blend
                if (!HasParameter(controller, PARAM_SPEED))
                {
                    controller.AddParameter(PARAM_SPEED, AnimatorControllerParameterType.Float);
                    modified = true;
                }

                // Attack (Trigger)
                if (!HasParameter(controller, PARAM_ATTACK))
                {
                    controller.AddParameter(PARAM_ATTACK, AnimatorControllerParameterType.Trigger);
                    modified = true;
                }

                // Hit (Trigger) - 被ダメージ
                if (!HasParameter(controller, PARAM_HIT))
                {
                    controller.AddParameter(PARAM_HIT, AnimatorControllerParameterType.Trigger);
                    modified = true;
                }

                // Death (Trigger) - 死亡
                if (!HasParameter(controller, PARAM_DEATH))
                {
                    controller.AddParameter(PARAM_DEATH, AnimatorControllerParameterType.Trigger);
                    modified = true;
                }

                if (modified)
                {
                    EditorUtility.SetDirty(controller);
                    updatedCount++;
                    Debug.Log($"[SurvivorAnimatorSetup] Added missing parameters to: {controller.name}");
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[SurvivorAnimatorSetup] Parameter check complete: {updatedCount} controllers updated.");
            EditorUtility.DisplayDialog("Animator Parameter Fix",
                $"Updated {updatedCount} animator controllers.\n\nAdded missing: Speed, Attack, Hit, Death", "OK");
        }

        private static bool HasParameter(AnimatorController controller, string name)
        {
            foreach (var param in controller.parameters)
            {
                if (param.name == name) return true;
            }
            return false;
        }

        private static List<string> GetAllEnemyAnimatorControllerPaths()
        {
            var paths = new List<string>();
            var guids = AssetDatabase.FindAssets("t:AnimatorController",
                new[] { "Assets/ProjectAssets/Survivor/Prefabs/Enemy" });

            foreach (var guid in guids)
            {
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }

            return paths;
        }

        [MenuItem("Tools/Survivor/Rebuild All Enemy Animators")]
        public static void RebuildAllEnemyAnimators()
        {
            var configs = GetEnemyConfigs();

            // 確認ダイアログ
            bool proceed = EditorUtility.DisplayDialog(
                "Rebuild All Enemy Animators",
                $"This will completely rebuild {configs.Count} animator controllers.\n\n" +
                "All existing states and transitions will be replaced.\n\n" +
                "Are you sure you want to proceed?",
                "Rebuild All",
                "Cancel");

            if (!proceed) return;

            int successCount = 0;

            foreach (var config in configs)
            {
                if (SetupAnimatorController(config))
                {
                    successCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SurvivorAnimatorSetup] Rebuild complete: {successCount}/{configs.Count} animators configured.");
            EditorUtility.DisplayDialog(
                "Rebuild Complete",
                $"Successfully rebuilt {successCount}/{configs.Count} animator controllers.",
                "OK");
        }

        [MenuItem("Tools/Survivor/Setup All Enemy Animators")]
        public static void SetupAllEnemyAnimators()
        {
            var configs = GetEnemyConfigs();
            int successCount = 0;

            foreach (var config in configs)
            {
                if (SetupAnimatorController(config))
                {
                    successCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SurvivorAnimatorSetup] Setup complete: {successCount}/{configs.Count} animators configured.");
        }

        [MenuItem("Tools/Survivor/Setup Selected Animator")]
        public static void SetupSelectedAnimator()
        {
            var selected = Selection.activeObject as AnimatorController;
            if (selected == null)
            {
                Debug.LogError("[SurvivorAnimatorSetup] Please select an AnimatorController asset.");
                return;
            }

            // パラメータのみ設定（アニメーションクリップは手動で設定）
            SetupParameters(selected);
            SetupBasicStructure(selected);

            AssetDatabase.SaveAssets();
            Debug.Log($"[SurvivorAnimatorSetup] Basic structure set up for: {selected.name}");
        }

        private static List<EnemyAnimationConfig> GetEnemyConfigs()
        {
            return new List<EnemyAnimationConfig>
            {
                // Slime
                new EnemyAnimationConfig
                {
                    ControllerPath = "Assets/ProjectAssets/Survivor/Prefabs/Enemy/Slime/Slime.controller",
                    IdleClipPath = "Assets/StoreAssets/DungeonMason/RPG Monster DUO PBR Polyart/Animations/Slime/IdleNormal_Slime_Anim.fbx",
                    RunClipPath = "Assets/StoreAssets/DungeonMason/RPG Monster DUO PBR Polyart/Animations/Slime/Run_Slime_Anim.fbx",
                    AttackClipPath = "Assets/StoreAssets/DungeonMason/RPG Monster DUO PBR Polyart/Animations/Slime/Attack01_Slime_Anim.fbx",
                    HitClipPath = "Assets/StoreAssets/DungeonMason/RPG Monster DUO PBR Polyart/Animations/Slime/GetHit_Slime_Anim.fbx",
                    DeathClipPath = "Assets/StoreAssets/DungeonMason/RPG Monster DUO PBR Polyart/Animations/Slime/Die_Slime_Anim.fbx"
                },
                // TurtleShell
                new EnemyAnimationConfig
                {
                    ControllerPath = "Assets/ProjectAssets/Survivor/Prefabs/Enemy/TurtleShell/TurtleShell.controller",
                    IdleClipPath = "Assets/StoreAssets/DungeonMason/RPG Monster DUO PBR Polyart/Animations/TurtleShell/IdleNormal_TurtleShell_Anim.fbx",
                    RunClipPath = "Assets/StoreAssets/DungeonMason/RPG Monster DUO PBR Polyart/Animations/TurtleShell/Run_TurtleShell_Anim.fbx",
                    AttackClipPath = "Assets/StoreAssets/DungeonMason/RPG Monster DUO PBR Polyart/Animations/TurtleShell/Attack01_TurtleShell_Anim.fbx",
                    HitClipPath = "Assets/StoreAssets/DungeonMason/RPG Monster DUO PBR Polyart/Animations/TurtleShell/GetHit_TurtleShell_Anim.fbx",
                    DeathClipPath = "Assets/StoreAssets/DungeonMason/RPG Monster DUO PBR Polyart/Animations/TurtleShell/Die_TurtleShell_Anim.fbx"
                },
                // Cactus
                new EnemyAnimationConfig
                {
                    ControllerPath = "Assets/ProjectAssets/Survivor/Prefabs/Enemy/Cactus/Cactus.controller",
                    IdleClipPath = "Assets/StoreAssets/DungeonMason/RPGMonsterBuddiesPBRPA/Animation/Cactus/Cactus_IdleNormal.fbx",
                    RunClipPath = "Assets/StoreAssets/DungeonMason/RPGMonsterBuddiesPBRPA/Animation/Cactus/Cactus_RunFWD.fbx",
                    AttackClipPath = "Assets/StoreAssets/DungeonMason/RPGMonsterBuddiesPBRPA/Animation/Cactus/Cactus_Attack01.fbx",
                    HitClipPath = "Assets/StoreAssets/DungeonMason/RPGMonsterBuddiesPBRPA/Animation/Cactus/Cactus_GetHit.fbx",
                    DeathClipPath = "Assets/StoreAssets/DungeonMason/RPGMonsterBuddiesPBRPA/Animation/Cactus/Cactus_Die.fbx"
                },
                // MushroomAngry
                new EnemyAnimationConfig
                {
                    ControllerPath = "Assets/ProjectAssets/Survivor/Prefabs/Enemy/Mushroom/MushroomAngry.controller",
                    IdleClipPath = "Assets/StoreAssets/DungeonMason/RPGMonsterBuddiesPBRPA/Animation/Mushroom/Mushroom_IdleNormalAngry.fbx",
                    RunClipPath = "Assets/StoreAssets/DungeonMason/RPGMonsterBuddiesPBRPA/Animation/Mushroom/Mushroom_runFWDAngry.fbx",
                    AttackClipPath = "Assets/StoreAssets/DungeonMason/RPGMonsterBuddiesPBRPA/Animation/Mushroom/Mushroom_Attack01Angry.fbx",
                    HitClipPath = "Assets/StoreAssets/DungeonMason/RPGMonsterBuddiesPBRPA/Animation/Mushroom/Mushroom_GetHitAngry.fbx",
                    DeathClipPath = "Assets/StoreAssets/DungeonMason/RPGMonsterBuddiesPBRPA/Animation/Mushroom/Mushroom_DieAngry.fbx"
                },
                // MushroomSmile
                new EnemyAnimationConfig
                {
                    ControllerPath = "Assets/ProjectAssets/Survivor/Prefabs/Enemy/Mushroom/MushroomSmile.controller",
                    IdleClipPath = "Assets/StoreAssets/DungeonMason/RPGMonsterBuddiesPBRPA/Animation/Mushroom/Mushroom_IdleNormalSmile.fbx",
                    RunClipPath = "Assets/StoreAssets/DungeonMason/RPGMonsterBuddiesPBRPA/Animation/Mushroom/Mushroom_runFWDSmile.fbx",
                    AttackClipPath = "Assets/StoreAssets/DungeonMason/RPGMonsterBuddiesPBRPA/Animation/Mushroom/Mushroom_Attack01Smile.fbx",
                    HitClipPath = "Assets/StoreAssets/DungeonMason/RPGMonsterBuddiesPBRPA/Animation/Mushroom/Mushroom_GetHitSmile.fbx",
                    DeathClipPath = "Assets/StoreAssets/DungeonMason/RPGMonsterBuddiesPBRPA/Animation/Mushroom/Mushroom_DieSmile.fbx"
                },
                // Beholder
                new EnemyAnimationConfig
                {
                    ControllerPath = "Assets/ProjectAssets/Survivor/Prefabs/Enemy/Beholder/Beholder.controller",
                    IdleClipPath = "Assets/StoreAssets/DungeonMason/RPGMonsterPartnersPBRPolyart/Animations/Beholder/IdleNormal.fbx",
                    RunClipPath = "Assets/StoreAssets/DungeonMason/RPGMonsterPartnersPBRPolyart/Animations/Beholder/Run.fbx",
                    AttackClipPath = "Assets/StoreAssets/DungeonMason/RPGMonsterPartnersPBRPolyart/Animations/Beholder/Attack01.fbx",
                    HitClipPath = "Assets/StoreAssets/DungeonMason/RPGMonsterPartnersPBRPolyart/Animations/Beholder/GetHit.fbx",
                    DeathClipPath = "Assets/StoreAssets/DungeonMason/RPGMonsterPartnersPBRPolyart/Animations/Beholder/Die.fbx"
                },
                // ChestMonster (フォルダ名にスペースあり)
                new EnemyAnimationConfig
                {
                    ControllerPath = "Assets/ProjectAssets/Survivor/Prefabs/Enemy/ChestMonster/ChestMonster.controller",
                    IdleClipPath = "Assets/StoreAssets/DungeonMason/RPGMonsterPartnersPBRPolyart/Animations/Chest Monster/IdleNormal.fbx",
                    RunClipPath = "Assets/StoreAssets/DungeonMason/RPGMonsterPartnersPBRPolyart/Animations/Chest Monster/Run.fbx",
                    AttackClipPath = "Assets/StoreAssets/DungeonMason/RPGMonsterPartnersPBRPolyart/Animations/Chest Monster/Attack01.fbx",
                    HitClipPath = "Assets/StoreAssets/DungeonMason/RPGMonsterPartnersPBRPolyart/Animations/Chest Monster/GetHit.fbx",
                    DeathClipPath = "Assets/StoreAssets/DungeonMason/RPGMonsterPartnersPBRPolyart/Animations/Chest Monster/Die.fbx"
                },
                // Swarm08
                new EnemyAnimationConfig
                {
                    ControllerPath = "Assets/ProjectAssets/Survivor/Prefabs/Enemy/Swarm/Swarm08.controller",
                    IdleClipPath = "Assets/StoreAssets/DungeonMason/Monster Minion Survivor PBR Polyart/Animation/Swarm08/Swarm08_Idle.fbx",
                    RunClipPath = "Assets/StoreAssets/DungeonMason/Monster Minion Survivor PBR Polyart/Animation/Swarm08/Swarm08_MoveFWD.fbx",
                    AttackClipPath = "Assets/StoreAssets/DungeonMason/Monster Minion Survivor PBR Polyart/Animation/Swarm08/Swarm08_Attack.fbx",
                    HitClipPath = "Assets/StoreAssets/DungeonMason/Monster Minion Survivor PBR Polyart/Animation/Swarm08/Swarm08_GetHit.fbx",
                    DeathClipPath = "Assets/StoreAssets/DungeonMason/Monster Minion Survivor PBR Polyart/Animation/Swarm08/Swarm08_Die.fbx"
                },
                // Swarm09
                new EnemyAnimationConfig
                {
                    ControllerPath = "Assets/ProjectAssets/Survivor/Prefabs/Enemy/Swarm/Swarm09.controller",
                    IdleClipPath = "Assets/StoreAssets/DungeonMason/Monster Minion Survivor PBR Polyart/Animation/Swarm09/Swarm09_Idle.fbx",
                    RunClipPath = "Assets/StoreAssets/DungeonMason/Monster Minion Survivor PBR Polyart/Animation/Swarm09/Swarm09_MoveFWD.fbx",
                    AttackClipPath = "Assets/StoreAssets/DungeonMason/Monster Minion Survivor PBR Polyart/Animation/Swarm09/Swarm09_Attack.fbx",
                    HitClipPath = "Assets/StoreAssets/DungeonMason/Monster Minion Survivor PBR Polyart/Animation/Swarm09/Swarm09_GetHit.fbx",
                    DeathClipPath = "Assets/StoreAssets/DungeonMason/Monster Minion Survivor PBR Polyart/Animation/Swarm09/Swarm09_Die.fbx"
                },
                // PartyMonster (共通アニメーション)
                new EnemyAnimationConfig
                {
                    ControllerPath = "Assets/ProjectAssets/Survivor/Prefabs/Enemy/PartyMonster/PartyMonster.controller",
                    IdleClipPath = "Assets/StoreAssets/DungeonMason/PartyMonsterDuo/Animation/Idle03.fbx",
                    RunClipPath = "Assets/StoreAssets/DungeonMason/PartyMonsterDuo/Animation/Run01FWD.fbx",
                    AttackClipPath = "Assets/StoreAssets/DungeonMason/PartyMonsterDuo/Animation/Attack01.fbx",
                    HitClipPath = "Assets/StoreAssets/DungeonMason/PartyMonsterDuo/Animation/GetHit.fbx",
                    DeathClipPath = "Assets/StoreAssets/DungeonMason/PartyMonsterDuo/Animation/Die01.fbx"
                },

                // ==================== Boss Dragons ====================

                // DragonSoulEater
                new EnemyAnimationConfig
                {
                    ControllerPath = "Assets/ProjectAssets/Survivor/Prefabs/Enemy/DragonSoulEater/SouleaterCTRL.controller",
                    IdleClipPath = "Assets/StoreAssets/DungeonMason/FourEvilDragonsHP/Animations/DragonSoulEater/Idle.fbx",
                    RunClipPath = "Assets/StoreAssets/DungeonMason/FourEvilDragonsHP/Animations/DragonSoulEater/Run.fbx",
                    AttackClipPath = "Assets/StoreAssets/DungeonMason/FourEvilDragonsHP/Animations/DragonSoulEater/Basic Attack.fbx",
                    HitClipPath = "Assets/StoreAssets/DungeonMason/FourEvilDragonsHP/Animations/DragonSoulEater/Get Hit.fbx",
                    DeathClipPath = "Assets/StoreAssets/DungeonMason/FourEvilDragonsHP/Animations/DragonSoulEater/Die.fbx"
                },
                // DragonNightmare
                new EnemyAnimationConfig
                {
                    ControllerPath = "Assets/ProjectAssets/Survivor/Prefabs/Enemy/DragonNightmare/NightmareCTRL.controller",
                    IdleClipPath = "Assets/StoreAssets/DungeonMason/FourEvilDragonsHP/Animations/DragonNightMare/idle01.fbx",
                    RunClipPath = "Assets/StoreAssets/DungeonMason/FourEvilDragonsHP/Animations/DragonNightMare/run.fbx",
                    AttackClipPath = "Assets/StoreAssets/DungeonMason/FourEvilDragonsHP/Animations/DragonNightMare/Basic Attack.fbx",
                    HitClipPath = "Assets/StoreAssets/DungeonMason/FourEvilDragonsHP/Animations/DragonNightMare/getHit.fbx",
                    DeathClipPath = "Assets/StoreAssets/DungeonMason/FourEvilDragonsHP/Animations/DragonNightMare/die.fbx"
                },
                // DragonTerrorBringer
                new EnemyAnimationConfig
                {
                    ControllerPath = "Assets/ProjectAssets/Survivor/Prefabs/Enemy/DragonTerrorBringer/TerrorbringerCTRL.controller",
                    IdleClipPath = "Assets/StoreAssets/DungeonMason/FourEvilDragonsHP/Animations/DragonTerrorBringer/idle01.fbx",
                    RunClipPath = "Assets/StoreAssets/DungeonMason/FourEvilDragonsHP/Animations/DragonTerrorBringer/Run.fbx",
                    AttackClipPath = "Assets/StoreAssets/DungeonMason/FourEvilDragonsHP/Animations/DragonTerrorBringer/Basic Attack.fbx",
                    HitClipPath = "Assets/StoreAssets/DungeonMason/FourEvilDragonsHP/Animations/DragonTerrorBringer/GetHit.fbx",
                    DeathClipPath = "Assets/StoreAssets/DungeonMason/FourEvilDragonsHP/Animations/DragonTerrorBringer/die.fbx"
                },
                // DragonUsurper
                new EnemyAnimationConfig
                {
                    ControllerPath = "Assets/ProjectAssets/Survivor/Prefabs/Enemy/DragonUsurper/UsurperCTRL.controller",
                    IdleClipPath = "Assets/StoreAssets/DungeonMason/FourEvilDragonsHP/Animations/DragonUsurper/idle01.fbx",
                    RunClipPath = "Assets/StoreAssets/DungeonMason/FourEvilDragonsHP/Animations/DragonUsurper/Run.fbx",
                    AttackClipPath = "Assets/StoreAssets/DungeonMason/FourEvilDragonsHP/Animations/DragonUsurper/attackMouth.fbx",
                    HitClipPath = "Assets/StoreAssets/DungeonMason/FourEvilDragonsHP/Animations/DragonUsurper/getHit.fbx",
                    DeathClipPath = "Assets/StoreAssets/DungeonMason/FourEvilDragonsHP/Animations/DragonUsurper/Die.fbx"
                }
            };
        }

        private static bool SetupAnimatorController(EnemyAnimationConfig config)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(config.ControllerPath);
            if (controller == null)
            {
                Debug.LogWarning($"[SurvivorAnimatorSetup] Controller not found: {config.ControllerPath}");
                return false;
            }

            // アニメーションクリップをロード
            var idleClip = LoadAnimationClip(config.IdleClipPath);
            var runClip = LoadAnimationClip(config.RunClipPath);
            var attackClip = LoadAnimationClip(config.AttackClipPath);
            var hitClip = LoadAnimationClip(config.HitClipPath);
            var deathClip = LoadAnimationClip(config.DeathClipPath);

            // パラメータ設定
            SetupParameters(controller);

            // 既存のステートとトランジションをクリア
            ClearControllerStates(controller);

            // ステート作成
            var rootStateMachine = controller.layers[0].stateMachine;

            // Locomotion BlendTree
            var locomotionState = CreateLocomotionBlendTree(controller, rootStateMachine, idleClip, runClip);

            // Attack State
            var attackState = rootStateMachine.AddState("Attack", new Vector3(300, 100, 0));
            if (attackClip != null) attackState.motion = attackClip;

            // Hit State
            var hitState = rootStateMachine.AddState("GetHit", new Vector3(300, 200, 0));
            if (hitClip != null) hitState.motion = hitClip;

            // Death State
            var deathState = rootStateMachine.AddState("Death", new Vector3(500, 0, 0));
            if (deathClip != null) deathState.motion = deathClip;

            // トランジション設定
            SetupTransitions(rootStateMachine, locomotionState, attackState, hitState, deathState);

            // デフォルトステート設定
            rootStateMachine.defaultState = locomotionState;

            EditorUtility.SetDirty(controller);
            Debug.Log($"[SurvivorAnimatorSetup] Configured: {controller.name}");
            return true;
        }

        private static AnimationClip LoadAnimationClip(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            // FBXからアニメーションクリップを取得
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var asset in assets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    return clip;
                }
            }

            Debug.LogWarning($"[SurvivorAnimatorSetup] Animation clip not found in: {path}");
            return null;
        }

        private static void SetupParameters(AnimatorController controller)
        {
            // 既存パラメータをチェックして追加
            AddParameterIfNotExists(controller, PARAM_SPEED, AnimatorControllerParameterType.Float);
            AddParameterIfNotExists(controller, PARAM_ATTACK, AnimatorControllerParameterType.Trigger);
            AddParameterIfNotExists(controller, PARAM_HIT, AnimatorControllerParameterType.Trigger);
            AddParameterIfNotExists(controller, PARAM_DEATH, AnimatorControllerParameterType.Trigger);
        }

        private static void AddParameterIfNotExists(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            foreach (var param in controller.parameters)
            {
                if (param.name == name) return;
            }
            controller.AddParameter(name, type);
        }

        private static void ClearControllerStates(AnimatorController controller)
        {
            var stateMachine = controller.layers[0].stateMachine;

            // 全てのステートを削除
            var states = stateMachine.states;
            foreach (var state in states)
            {
                stateMachine.RemoveState(state.state);
            }

            // Any State transitionsもクリア
            var anyTransitions = stateMachine.anyStateTransitions;
            foreach (var transition in anyTransitions)
            {
                stateMachine.RemoveAnyStateTransition(transition);
            }
        }

        private static AnimatorState CreateLocomotionBlendTree(
            AnimatorController controller,
            AnimatorStateMachine stateMachine,
            AnimationClip idleClip,
            AnimationClip runClip)
        {
            // BlendTreeを作成
            var locomotionState = stateMachine.AddState("Locomotion", new Vector3(300, 0, 0));

            var blendTree = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = PARAM_SPEED,
                useAutomaticThresholds = false
            };

            // クリップを追加
            if (idleClip != null)
            {
                blendTree.AddChild(idleClip, 0f);
            }
            if (runClip != null)
            {
                blendTree.AddChild(runClip, 1f);
            }

            // BlendTreeをアセットに追加
            AssetDatabase.AddObjectToAsset(blendTree, controller);
            locomotionState.motion = blendTree;

            return locomotionState;
        }

        private static void SetupTransitions(
            AnimatorStateMachine stateMachine,
            AnimatorState locomotionState,
            AnimatorState attackState,
            AnimatorState hitState,
            AnimatorState deathState)
        {
            // Locomotion -> Attack (Attack trigger)
            var toAttack = locomotionState.AddTransition(attackState);
            toAttack.AddCondition(AnimatorConditionMode.If, 0, PARAM_ATTACK);
            toAttack.hasExitTime = false;
            toAttack.duration = 0.1f;

            // Attack -> Locomotion (has exit time)
            var fromAttack = attackState.AddTransition(locomotionState);
            fromAttack.hasExitTime = true;
            fromAttack.exitTime = 0.9f;
            fromAttack.duration = 0.1f;

            // Locomotion -> GetHit (Hit trigger)
            var toHit = locomotionState.AddTransition(hitState);
            toHit.AddCondition(AnimatorConditionMode.If, 0, PARAM_HIT);
            toHit.hasExitTime = false;
            toHit.duration = 0.05f;

            // Attack -> GetHit (Hit trigger, 攻撃中でも被弾)
            var attackToHit = attackState.AddTransition(hitState);
            attackToHit.AddCondition(AnimatorConditionMode.If, 0, PARAM_HIT);
            attackToHit.hasExitTime = false;
            attackToHit.duration = 0.05f;

            // GetHit -> Locomotion (has exit time)
            var fromHit = hitState.AddTransition(locomotionState);
            fromHit.hasExitTime = true;
            fromHit.exitTime = 0.8f;
            fromHit.duration = 0.1f;

            // Any State -> Death (Death trigger)
            var toDeath = stateMachine.AddAnyStateTransition(deathState);
            toDeath.AddCondition(AnimatorConditionMode.If, 0, PARAM_DEATH);
            toDeath.hasExitTime = false;
            toDeath.duration = 0.1f;
        }

        private static void SetupBasicStructure(AnimatorController controller)
        {
            // 既存のステートとトランジションをクリア
            ClearControllerStates(controller);

            var rootStateMachine = controller.layers[0].stateMachine;

            // 空のステートを作成（アニメーションは手動で設定）
            var locomotionState = rootStateMachine.AddState("Locomotion", new Vector3(300, 0, 0));
            var attackState = rootStateMachine.AddState("Attack", new Vector3(300, 100, 0));
            var hitState = rootStateMachine.AddState("GetHit", new Vector3(300, 200, 0));
            var deathState = rootStateMachine.AddState("Death", new Vector3(500, 0, 0));

            // トランジション設定
            SetupTransitions(rootStateMachine, locomotionState, attackState, hitState, deathState);

            // デフォルトステート設定
            rootStateMachine.defaultState = locomotionState;

            EditorUtility.SetDirty(controller);
        }
    }
}
