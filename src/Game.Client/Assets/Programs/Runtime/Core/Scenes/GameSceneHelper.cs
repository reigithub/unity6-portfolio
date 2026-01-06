using System;
using System.Collections.Generic;
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
    }
}