using System.Collections.Generic;
using Game.Shared.Constants;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Shared.Scenes
{
    public static class GameSceneHelper
    {
        public static Scene GetGameRootScene()
        {
            return SceneManager.GetSceneByName(AppConstants.GameRootScene);
        }

        public static void MoveToGameRootScene(GameObject scene)
        {
            var rootScene = GetGameRootScene();
            if (rootScene.IsValid())
            {
                SceneManager.MoveGameObjectToScene(scene, rootScene);
            }
        }

        public static T GetSceneComponent<T>(GameObject scene)
        {
            if (scene.TryGetComponent<T>(out var sceneComponent))
            {
                return sceneComponent;
            }

            return scene.GetComponentInChildren<T>();
        }

        public static T GetSceneComponent<T>(Scene scene)
        {
            var rootGameObjects = scene.GetRootGameObjects();

            foreach (var obj in rootGameObjects)
            {
                if (obj.TryGetComponent<T>(out var component))
                {
                    return component;
                }
            }

            return default;
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
    }
}
