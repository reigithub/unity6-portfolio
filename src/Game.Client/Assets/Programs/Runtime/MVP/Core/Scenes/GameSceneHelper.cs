using System;
using System.Collections.Generic;
using Game.MVP.Core.Constants;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.MVP.Core.Scenes
{
    public static class GameSceneHelper
    {
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

        public static T GetComponent<T>(GameObject gameObject) where T : Behaviour
        {
            if (gameObject.TryGetComponent<T>(out var component))
            {
                return component;
            }

            return gameObject.GetComponentInChildren<T>();
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