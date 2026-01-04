using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Game.Contents.Enemy;
using Game.Contents.Item;
using Game.Contents.Player;
using Game.Core.Constants;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core.Scenes
{
    public static class GameSceneHelper
    {
        public static GameScene CreateInstance(Type type)
        {
            try
            {
                var scene = Activator.CreateInstance(type) as GameScene;
                return scene;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.Assert(true, $"{type}\n{e.Message}");
                return null;
            }
        }

        public static TScene CreateInstance<TScene>()
        {
            try
            {
                var scene = Activator.CreateInstance(typeof(TScene));
                if (scene is TScene t) return t;
                return default;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.Assert(true, $"{typeof(TScene)}\n{e.Message}");
                return default;
            }
        }

        public static void MoveToGameRootScene(GameObject scene)
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.name == GameSceneConstants.GameRootScene)
            {
                SceneManager.MoveGameObjectToScene(scene, activeScene);
            }
            else
            {
                var rootScene = SceneManager.GetSceneByName(GameSceneConstants.GameRootScene);
                if (rootScene.IsValid())
                {
                    SceneManager.MoveGameObjectToScene(scene, rootScene);
                }
            }
        }

        public static T GetSceneComponent<T>(GameObject scene) where T : Behaviour
        {
            if (scene.TryGetComponent<T>(out var sceneComponent))
            {
                return sceneComponent;
            }

            return scene.GetComponentInChildren<T>();
        }

        public static T GetSceneComponent<T>(Scene scene) where T : MonoBehaviour
        {
            return GetRootComponent<T>(scene);
        }

        public static T GetRootComponent<T>(Scene scene) where T : MonoBehaviour
        {
            var rootGameObjects = scene.GetRootGameObjects();

            T component = null;
            foreach (var obj in rootGameObjects)
            {
                if (obj.TryGetComponent<T>(out component))
                    break;
            }

            return component;
        }

        public static Skybox GetSkybox(Scene scene)
        {
            return GetComponentInChildren<Skybox>(scene);
        }

        /// <summary>
        /// ステージインスタンスからプレイヤー開始地点を探して、一番最初に見つかったものを返す
        /// WARN: 複数配置しないように注意…
        /// </summary>
        public static PlayerStart GetPlayerStart(Scene scene)
        {
            return GetComponentInChildren<PlayerStart>(scene);
        }

        public static EnemyStart[] GetEnemyStarts(Scene scene)
        {
            return GetComponentsInChildren<EnemyStart>(scene);
        }

        public static StageItemStart[] GetStageItemStarts(Scene scene)
        {
            return GetComponentsInChildren<StageItemStart>(scene);
        }

        public static T GetComponentInChildren<T>(Scene scene) where T : Behaviour
        {
            var rootGameObjects = scene.GetRootGameObjects();

            T component = null;
            foreach (var obj in rootGameObjects)
            {
                if (obj.TryGetComponent<T>(out component))
                    break;

                component = obj.GetComponentInChildren<T>();
                if (component != null)
                    break;
            }

            return component;
        }

        public static T[] GetComponentsInChildren<T>(Scene scene) where T : Behaviour
        {
            var rootGameObjects = scene.GetRootGameObjects();

            var list = new List<T>();
            foreach (var obj in rootGameObjects)
            {
                if (obj.TryGetComponent<T>(out var component))
                {
                    list.Add(component);
                    continue;
                }

                var components = obj.GetComponentsInChildren<T>();
                if (components != null && components.Length > 0)
                {
                    list.AddRange(components);
                }
            }

            return list.ToArray();
        }

        // Memo: インターフェース周辺が複雑になり始めているので、雛形の基底シーンクラスを用意する、Type.GetInterfacesなどを検討
        // var interfaces = sceneType.GetInterfaces();
        // foreach (var interfaceType in interfaces)
        // {
        //     if (interfaceType == typeof(IGameSceneArg<>))
        //     {
        //         var mi = interfaceType.GetMethod("PreInitialize");
        //         var d = (Task)mi?.Invoke(instance, new object[] { });
        //         if (d != null)
        //         {
        //             await d;
        //         }
        //     }
        // }

        // public static MethodInfo GetMethod(Type sceneType, object instance, string methodName, params Type[] parameterTypes)
        // {
        // }

        // public static MethodInfo GetMethod(object instance, string methodName, params Type[] parameterTypes)
        // {
        //     if (instance == null)
        //         return null;
        //
        //     if (parameterTypes == null || parameterTypes.Length == 0)
        //     {
        //         return instance.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        //     }
        //     else
        //     {
        //         return instance.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, parameterTypes, null);
        //     }
        // }

        #region Obsolute

        // 以下Addressable経由でどうにかなりそうなので不要かも

        public static async Task<Scene> LoadUnitySceneAsync(string unitySceneName, LoadSceneMode loadMode)
        {
// #if UNITY_EDITOR
//             if (AssetLoadMode != AssetLoadMode.AssetBundle)
//             {
//                 var path = GetUnityScenePath(unitySceneName);
//                 var parameters = new LoadSceneParameters(loadMode);
//                 await UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path, parameters);
//                 return SceneManager.GetSceneByName(unitySceneName);
//             }
//             else
// #endif
            {
                await SceneManager.LoadSceneAsync(unitySceneName, loadMode);
                return SceneManager.GetSceneByName(unitySceneName);
            }
        }

        public static Task UnloadUnitySceneAsync(string unitySceneName)
        {
            var scene = SceneManager.GetSceneByName(unitySceneName);
            if (scene.IsValid())
            {
                return UnloadUnitySceneAsync(scene);
            }

            return Task.CompletedTask;
        }

        public static async Task UnloadUnitySceneAsync(Scene scene)
        {
            if (!scene.isLoaded)
                return;

            var operation = SceneManager.UnloadSceneAsync(scene);
            if (operation != null)
            {
                await operation;
            }
        }

#if UNITY_EDITOR
        public static string GetUnityScenePath(string unitySceneName)
        {
            string path = string.Empty;

            var guids = UnityEditor.AssetDatabase.FindAssets("t:Scene " + unitySceneName, new[] { "Assets/Scenes" });
            foreach (var guid in guids)
            {
                var assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var fileName = Path.GetFileNameWithoutExtension(assetPath);
                if (fileName == unitySceneName)
                {
                    path = assetPath;
                    break;
                }
            }

            return path;
        }
#endif

        #endregion
    }
}