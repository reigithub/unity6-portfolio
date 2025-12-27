using System;
using System.IO;
using System.Threading.Tasks;
using Game.Core.Constants;
using Sample;
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

        public static T GetSceneComponent<T>(GameObject scene) where T : GameSceneComponent
        {
            if (scene.TryGetComponent<T>(out var sceneComponent))
            {
                return sceneComponent;
            }

            return scene.GetComponentInChildren<T>();
        }

        public static T GetSceneComponent<T>(Scene scene) where T : GameSceneComponent
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

        public static PlayerStart GetPlayerStart(Scene scene)
        {
            return GetComponentInChildren<PlayerStart>(scene);
        }

        public static T GetComponentInChildren<T>(Scene scene) where T : MonoBehaviour
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
    }
}