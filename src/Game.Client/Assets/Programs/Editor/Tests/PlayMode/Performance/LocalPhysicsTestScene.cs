using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Tests.PlayMode.Performance
{
    /// <summary>
    /// テスト用の独立 PhysicsScene ラッパー。
    /// auto simulate に依存せず Simulate(dt) を明示呼び出しすることで、
    /// テスト間 / フレーム間の干渉を排除する。
    /// </summary>
    public class LocalPhysicsTestScene
    {
        public Scene Scene { get; }
        public PhysicsScene PhysicsScene { get; }

        public LocalPhysicsTestScene(string name)
        {
            var parameters = new CreateSceneParameters(LocalPhysicsMode.Physics3D);
            Scene = SceneManager.CreateScene(name, parameters);
            PhysicsScene = Scene.GetPhysicsScene();
        }

        public GameObject CreateGameObject(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            SceneManager.MoveGameObjectToScene(go, Scene);
            return go;
        }

        public void Simulate(float deltaTime)
        {
            PhysicsScene.Simulate(deltaTime);
        }

        /// <summary>
        /// コルーチン終了まで待ってから UnloadSceneAsync を呼ぶためのヘルパー。
        /// try-finally 内で yield できないため UnityTearDown で呼び出す形式を前提とする。
        /// </summary>
        public IEnumerator UnloadAsync()
        {
            if (!Scene.IsValid()) yield break;
            var op = SceneManager.UnloadSceneAsync(Scene);
            while (op != null && !op.isDone)
            {
                yield return null;
            }
        }
    }
}
