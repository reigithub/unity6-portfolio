using System;
using System.Collections.Generic;
using Game.Shared.Constants;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.MVC.Core.Scenes
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
            if (activeScene.IsValid() && activeScene.name == AppConstants.GameRootScene)
            {
                SceneManager.MoveGameObjectToScene(scene, activeScene);
            }
            else
            {
                var rootScene = SceneManager.GetSceneByName(AppConstants.GameRootScene);
                if (rootScene.IsValid())
                {
                    SceneManager.MoveGameObjectToScene(scene, rootScene);
                }
            }
        }

        public static T GetSceneComponent<T>(GameObject scene) where T : IGameSceneComponent
        {
            if (scene.TryGetComponent<T>(out var sceneComponent))
            {
                return sceneComponent;
            }

            return scene.GetComponentInChildren<T>();
        }

        public static T GetSceneComponent<T>(Scene scene) where T : IGameSceneComponent
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