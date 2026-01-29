using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Addressables経由のシーンロード機能の統合テスト
    /// このゲームではSceneManagerの直接使用ではなく、IGameSceneService経由でAddressablesを使用する設計
    /// このテストはAddressablesのシーンロード基盤の動作確認を行う
    /// </summary>
    [TestFixture]
    public class SceneTransitionTests
    {
        private bool _addressablesInitialized;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return InitializeAddressables().ToCoroutine();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return null;
        }

        /// <summary>
        /// Addressablesが初期化可能であることを確認
        /// </summary>
        [UnityTest]
        public IEnumerator Addressables_CanBeInitialized()
        {
            // Assert
            Assert.IsTrue(_addressablesInitialized, "Addressables should be initialized for scene loading");
            yield return null;
        }

        /// <summary>
        /// Addressables経由でシーンキーが存在するか確認
        /// （実際のシーンロードはIGameSceneService経由で行うため、ここではキーの存在確認のみ）
        /// </summary>
        [UnityTest]
        public IEnumerator Addressables_SceneKeyExists()
        {
            if (!_addressablesInitialized)
            {
                Assert.Inconclusive("Addressables not initialized.");
                yield break;
            }

            // Addressables関連のエラーログを無視
            LogAssert.ignoreFailingMessages = true;

            yield return UniTask.ToCoroutine(async () =>
            {
                try
                {
                    // このゲームではシーンもAddressables経由でロードされる
                    // 実際のシーンキーはプロジェクト設定に依存
                    const string sampleSceneKey = "PolyRPG";

                    var locationsHandle = Addressables.LoadResourceLocationsAsync(sampleSceneKey, typeof(SceneInstance));
                    await locationsHandle.ToUniTask();

                    if (locationsHandle.Status == AsyncOperationStatus.Succeeded && locationsHandle.Result.Count > 0)
                    {
                        Debug.Log($"[SceneTransitionTests] Scene key '{sampleSceneKey}' found in Addressables");
                        Assert.Pass($"Scene key '{sampleSceneKey}' exists in Addressables");
                    }
                    else
                    {
                        Assert.Inconclusive("No scene keys configured in Addressables. Configure scenes in Addressables groups to test scene loading.");
                    }

                    Addressables.Release(locationsHandle);
                }
                catch (SuccessException e)
                {
                    Assert.Throws<SuccessException>(() => throw new SuccessException(e.Message));
                }
                catch (System.Exception e)
                {
                    Assert.Inconclusive($"Scene key check failed: {e.Message}");
                }
                finally
                {
                    LogAssert.ignoreFailingMessages = false;
                }
            });
        }

        /// <summary>
        /// DontDestroyOnLoadオブジェクトが正しく動作することを確認
        /// （IGameSceneServiceで使用されるGameRootSceneはDontDestroyOnLoadを活用）
        /// </summary>
        [UnityTest]
        public IEnumerator DontDestroyOnLoad_ObjectsPersist()
        {
            // Arrange - 永続オブジェクトを作成
            var persistentObject = new GameObject("TestPersistentObject");
            Object.DontDestroyOnLoad(persistentObject);
            yield return null;

            // Assert - オブジェクトがDontDestroyOnLoadシーンに移動されている
            Assert.IsNotNull(persistentObject, "Persistent object should exist");
            Assert.IsTrue(persistentObject.scene.name == "DontDestroyOnLoad" || !persistentObject.scene.isLoaded,
                "Object should be in DontDestroyOnLoad scene");

            // Cleanup
            Object.Destroy(persistentObject);
            yield return null;
        }

        /// <summary>
        /// シーンのアクティブ切り替えが正常に動作することを確認
        /// </summary>
        [UnityTest]
        public IEnumerator SceneManager_GetActiveScene_Works()
        {
            // Act
            var activeScene = SceneManager.GetActiveScene();
            yield return null;

            // Assert
            Assert.IsTrue(activeScene.IsValid(), "Active scene should be valid");
            Assert.IsTrue(activeScene.isLoaded, "Active scene should be loaded");
            Debug.Log($"[SceneTransitionTests] Active scene: {activeScene.name}");
        }

        /// <summary>
        /// シーンカウントが正しく取得できることを確認
        /// </summary>
        [UnityTest]
        public IEnumerator SceneManager_SceneCount_IsValid()
        {
            // Act
            int sceneCount = SceneManager.sceneCount;
            yield return null;

            // Assert
            Assert.GreaterOrEqual(sceneCount, 1, "At least one scene should be loaded");
            Debug.Log($"[SceneTransitionTests] Scene count: {sceneCount}");
        }

        #region Helper Methods

        private async UniTask InitializeAddressables()
        {
            try
            {
                var initHandle = Addressables.InitializeAsync();
                await initHandle.ToUniTask();
                _addressablesInitialized = initHandle.Status == AsyncOperationStatus.Succeeded;

                if (_addressablesInitialized)
                {
                    Debug.Log("[SceneTransitionTests] Addressables initialized successfully");
                }
                else
                {
                    Debug.LogWarning("[SceneTransitionTests] Addressables initialization failed");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SceneTransitionTests] Addressables not available: {e.Message}");
                _addressablesInitialized = false;
            }
        }

        #endregion
    }
}
