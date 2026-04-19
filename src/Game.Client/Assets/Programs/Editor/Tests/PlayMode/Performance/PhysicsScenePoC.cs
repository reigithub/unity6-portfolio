using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.Performance
{
    /// <summary>
    /// Layer 2 Step 1a PoC:
    /// Unity 6 の `PhysicsScene.Create` + 独立 Collider 配置 + 明示 `Simulate`
    /// + `SphereCast` が Editor / Linux Headless CI で動作することを最小コードで確認する。
    ///
    /// このテストが pass しない場合、Layer 2 計画の `LocalPhysicsTestScene` 方針を放棄し、
    /// 単一 scene + `[NonParallelizable]` fallback に切り替える判断の根拠になる。
    /// </summary>
    [TestFixture]
    public class PhysicsScenePoC
    {
        private Scene _testScene;
        private bool _sceneCreated;
        private GameObject _target;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_target != null)
            {
                Object.Destroy(_target);
                _target = null;
            }

            if (_sceneCreated && _testScene.IsValid())
            {
                var unload = SceneManager.UnloadSceneAsync(_testScene);
                while (unload != null && !unload.isDone)
                {
                    yield return null;
                }
                _sceneCreated = false;
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator LocalPhysicsScene_SphereCast_HitsCollider()
        {
            var parameters = new CreateSceneParameters(LocalPhysicsMode.Physics3D);
            _testScene = SceneManager.CreateScene("PerfTestPhysicsPoC", parameters);
            _sceneCreated = true;
            var physicsScene = _testScene.GetPhysicsScene();

            _target = new GameObject("PoCTarget");
            SceneManager.MoveGameObjectToScene(_target, _testScene);
            _target.transform.position = new Vector3(0f, 0f, 5f);
            var collider = _target.AddComponent<SphereCollider>();
            collider.radius = 1f;

            // auto simulate に依存せず明示 Simulate で Collider を同期
            physicsScene.Simulate(0.02f);
            yield return null;

            bool hit = physicsScene.SphereCast(
                origin: new Vector3(0f, 0f, 0f),
                radius: 0.5f,
                direction: Vector3.forward,
                hitInfo: out RaycastHit hitInfo,
                maxDistance: 10f);

            Assert.IsTrue(hit, "PhysicsScene.SphereCast が独立シーンの Collider を検出できませんでした。");
            Assert.AreEqual(_target, hitInfo.collider.gameObject,
                "SphereCast が期待した GameObject を返しませんでした。");
        }
    }
}
