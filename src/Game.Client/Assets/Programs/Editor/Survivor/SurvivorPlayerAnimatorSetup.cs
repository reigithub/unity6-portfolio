using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Editor.Survivor
{
    /// <summary>
    /// Survivorプレイヤー用AnimatorControllerを設定するエディタツール
    /// </summary>
    public static class SurvivorPlayerAnimatorSetup
    {
        private const string AnimatorControllerPath = "Assets/ProjectAssets/Survivor/Prefabs/Player/SDUnityChan.controller";
        // アニメーションが埋め込まれたFBX
        private const string MotionFbxPath = "Assets/UnityChan/SD Unity-chan 3D Model Data/Animators/SD_unitychan_motion_humanoid.fbx";

        [MenuItem("Project/Survivor/Setup Player Animator Controller")]
        public static void SetupAnimatorController()
        {
            // 既存のコントローラーを読み込むか、新規作成
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(AnimatorControllerPath);
                Debug.Log($"Created new AnimatorController at {AnimatorControllerPath}");
            }

            // パラメータをクリアして再設定
            ClearParameters(controller);

            // 必要なパラメータを追加
            // Speed: 移動速度（BlendTree制御 + 遷移条件）
            // Death: 死亡トリガー
            // ※ IsMovingは Speed > 0 で代替可能なため不要
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

            // FBXからアニメーションクリップを取得（SD_unitychan_motion_humanoid.fbx）
            var idleClip = FindAnimationClipInFbx(MotionFbxPath, "Standing@loop");
            var walkClip = FindAnimationClipInFbx(MotionFbxPath, "Walking@loop");
            var runClip = FindAnimationClipInFbx(MotionFbxPath, "Running@loop");
            var deathClip = FindAnimationClipInFbx(MotionFbxPath, "GoDown");
            var damagedClip = FindAnimationClipInFbx(MotionFbxPath, "Damaged@loop");

            Debug.Log($"Found clips - Idle: {idleClip?.name}, Walk: {walkClip?.name}, Run: {runClip?.name}, Death: {deathClip?.name}");

            if (idleClip == null || walkClip == null || runClip == null)
            {
                Debug.LogError("Required animation clips not found in FBX!");
                return;
            }

            // ステートマシンを取得
            var rootStateMachine = controller.layers[0].stateMachine;

            // 既存のステートをクリア
            ClearStates(rootStateMachine);

            // Idle State
            var idleState = rootStateMachine.AddState("Idle", new Vector3(300, 100, 0));
            if (idleClip != null)
            {
                idleState.motion = idleClip;
            }
            rootStateMachine.defaultState = idleState;

            // Locomotion State (BlendTree)
            // サバイバーゲームでは歩きは不要、ジョギング(5)とダッシュ(7.5)でRunアニメーション
            var locomotionState = rootStateMachine.AddState("Locomotion", new Vector3(300, 200, 0));

            // BlendTreeを作成（実速度ベース: 0=idle, 5=jog, 8=dash）
            var blendTree = new BlendTree
            {
                name = "Locomotion",
                blendParameter = "Speed",
                blendType = BlendTreeType.Simple1D
            };

            // 閾値を実際の移動速度に合わせる
            // 0: 静止 → Standing
            // 3: 低速移動開始 → Running開始
            // 5: ジョギング → Running
            // 8: ダッシュ → Running（同じアニメーション）
            if (idleClip != null)
                blendTree.AddChild(idleClip, 0f);
            if (runClip != null)
                blendTree.AddChild(runClip, 3f);   // 低速でもRunアニメーション
            if (runClip != null)
                blendTree.AddChild(runClip, 8f);   // ダッシュ時もRunアニメーション

            locomotionState.motion = blendTree;

            // Death State
            var deathState = rootStateMachine.AddState("Death", new Vector3(500, 150, 0));
            if (deathClip != null)
            {
                deathState.motion = deathClip;
            }

            // Transitions（実速度ベースの閾値）
            // Idle -> Locomotion (Speed > 0.5)
            var idleToLocomotion = idleState.AddTransition(locomotionState);
            idleToLocomotion.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed");
            idleToLocomotion.duration = 0.1f;
            idleToLocomotion.hasExitTime = false;

            // Locomotion -> Idle (Speed < 0.5)
            var locomotionToIdle = locomotionState.AddTransition(idleState);
            locomotionToIdle.AddCondition(AnimatorConditionMode.Less, 0.5f, "Speed");
            locomotionToIdle.duration = 0.1f;
            locomotionToIdle.hasExitTime = false;

            // Any State -> Death (Death trigger)
            var anyToDeath = rootStateMachine.AddAnyStateTransition(deathState);
            anyToDeath.AddCondition(AnimatorConditionMode.If, 0, "Death");
            anyToDeath.duration = 0.1f;
            anyToDeath.hasExitTime = false;

            // BlendTreeをアセットに追加
            AssetDatabase.AddObjectToAsset(blendTree, controller);

            // 保存
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"AnimatorController setup complete: {AnimatorControllerPath}");
            Debug.Log($"Parameters: Speed (float - actual speed value), Death (trigger)");
            Debug.Log($"States: Idle, Locomotion (BlendTree: 0=Standing, 3-8=Running), Death");
            Debug.Log($"Transitions: Idle <-> Locomotion (Speed threshold 0.5), Any -> Death (trigger)");

            // 結果を表示
            Selection.activeObject = controller;
        }

        private static void ClearParameters(AnimatorController controller)
        {
            // パラメータを全て削除
            while (controller.parameters.Length > 0)
            {
                controller.RemoveParameter(0);
            }
        }

        private static void ClearStates(AnimatorStateMachine stateMachine)
        {
            // ステートを全て削除
            var states = stateMachine.states;
            foreach (var state in states)
            {
                stateMachine.RemoveState(state.state);
            }

            // サブステートマシンも削除
            var childMachines = stateMachine.stateMachines;
            foreach (var child in childMachines)
            {
                stateMachine.RemoveStateMachine(child.stateMachine);
            }
        }

        private static AnimationClip FindAnimationClipInFbx(string fbxPath, string clipName)
        {
            var objects = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (var obj in objects)
            {
                if (obj is AnimationClip clip && !clip.name.StartsWith("__preview") && clip.name == clipName)
                {
                    return clip;
                }
            }
            // 完全一致がない場合は部分一致を試す
            foreach (var obj in objects)
            {
                if (obj is AnimationClip clip && !clip.name.StartsWith("__preview") && clip.name.Contains(clipName))
                {
                    return clip;
                }
            }
            return null;
        }

        private static AnimationClip FindAnimationClipInProject(string searchTerm, string folderFilter)
        {
            var guids = AssetDatabase.FindAssets($"t:AnimationClip {searchTerm}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.ToLower().Contains(folderFilter.ToLower()))
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if (clip != null)
                    {
                        Debug.Log($"Found animation clip: {clip.name} at {path}");
                        return clip;
                    }
                }
            }
            return null;
        }

        [MenuItem("Project/Survivor/List Available UnityChan Animations")]
        public static void ListAvailableAnimations()
        {
            Debug.Log("=== Searching for UnityChan animations ===");

            // FBXから検索
            var fbxPaths = new[]
            {
                "Assets/UnityChan/SD Unity-chan 3D Model Data/Models/SD_unitychan_humanoid.fbx",
                "Assets/UnityChan/SD Unity-chan 3D Model Data/Models/SD_unitychan_generic.fbx"
            };

            foreach (var path in fbxPaths)
            {
                Debug.Log($"\n--- Animations in {path} ---");
                var objects = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (var obj in objects)
                {
                    if (obj is AnimationClip clip && !clip.name.StartsWith("__preview"))
                    {
                        Debug.Log($"  {clip.name} ({clip.length:F2}s, loop={clip.isLooping})");
                    }
                }
            }

            // プロジェクト全体からUnityChan関連のアニメーションを検索
            Debug.Log("\n--- AnimationClips in project containing 'unitychan' ---");
            var guids = AssetDatabase.FindAssets("t:AnimationClip");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.ToLower().Contains("unitychan"))
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if (clip != null)
                    {
                        Debug.Log($"  {clip.name} at {path}");
                    }
                }
            }
        }
    }
}
