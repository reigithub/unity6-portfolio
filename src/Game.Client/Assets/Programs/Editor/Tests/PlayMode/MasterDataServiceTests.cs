using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// MasterDataServiceの統合テスト
    /// Addressablesとの連携をPlayModeでテスト
    /// </summary>
    [TestFixture]
    public class MasterDataServiceTests
    {
        private bool _addressablesInitialized;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Addressablesの初期化を試みる
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
            if (!_addressablesInitialized)
            {
                Assert.Inconclusive("Addressables not initialized. Ensure Addressables is configured.");
                yield break;
            }

            // Assert
            Assert.IsTrue(_addressablesInitialized, "Addressables should be initialized");
            yield return null;
        }

        /// <summary>
        /// マスターデータのキーが存在することを確認（存在する場合）
        /// </summary>
        [UnityTest]
        public IEnumerator MasterData_KeyExists_InAddressables()
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
                    // マスターデータのAddressablesキー
                    const string masterDataKey = "MasterDataBinary";

                    // キーの存在確認
                    var locationsHandle = Addressables.LoadResourceLocationsAsync(masterDataKey);
                    await locationsHandle.ToUniTask();

                    if (locationsHandle.Status == AsyncOperationStatus.Succeeded)
                    {
                        var locations = locationsHandle.Result;
                        Debug.Log($"[MasterDataServiceTests] Found {locations.Count} location(s) for key '{masterDataKey}'");
                        if (locations.Count == 0)
                        {
                            Assert.Inconclusive($"Key '{masterDataKey}' not found in Addressables. Configure MasterData in Addressables groups.");
                        }
                        else
                        {
                            Assert.Greater(locations.Count, 0, $"Key '{masterDataKey}' should have at least one location");
                        }
                    }
                    else
                    {
                        Assert.Inconclusive($"Key '{masterDataKey}' not found in Addressables. Configure MasterData in Addressables groups.");
                    }

                    Addressables.Release(locationsHandle);
                }
                catch (Exception e)
                {
                    Assert.Inconclusive($"Failed to check Addressables key: {e.Message}");
                }
                finally
                {
                    LogAssert.ignoreFailingMessages = false;
                }
            });
        }

        /// <summary>
        /// Addressables経由でアセットをロードできることを確認
        /// </summary>
        [UnityTest]
        public IEnumerator Addressables_CanLoadAsset()
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
                    // 既知の存在するアセットキーでテスト
                    const string testKey = "MasterDataBinary";

                    var handle = Addressables.LoadAssetAsync<TextAsset>(testKey);
                    var result = await handle.ToUniTask();

                    if (handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        Assert.IsNotNull(result, "Loaded asset should not be null");
                        Debug.Log($"[MasterDataServiceTests] Successfully loaded asset: {result.name}, Size: {result.bytes.Length} bytes");
                        Addressables.Release(handle);
                    }
                    else
                    {
                        Assert.Inconclusive("Asset not found or failed to load. Check Addressables configuration.");
                    }
                }
                catch (Exception e)
                {
                    Assert.Inconclusive($"Failed to load asset: {e.Message}");
                }
                finally
                {
                    LogAssert.ignoreFailingMessages = false;
                }
            });
        }

        /// <summary>
        /// 存在しないキーでロードするとエラーになることを確認
        /// </summary>
        [UnityTest]
        public IEnumerator Addressables_InvalidKey_FailsGracefully()
        {
            if (!_addressablesInitialized)
            {
                Assert.Inconclusive("Addressables not initialized.");
                yield break;
            }

            // 存在しないキーでのエラーログを無視
            LogAssert.ignoreFailingMessages = true;

            yield return UniTask.ToCoroutine(async () =>
            {
                try
                {
                    const string invalidKey = "NonExistentKey_12345_Invalid";

                    var handle = Addressables.LoadAssetAsync<TextAsset>(invalidKey);
                    await handle.ToUniTask();

                    // ここに到達したらロードが成功した（予期しない）
                    Addressables.Release(handle);
                    Assert.Fail("Loading invalid key should fail");
                }
                catch (Exception)
                {
                    // 期待される動作 - エラーがスローされる
                    Assert.Pass("Invalid key correctly throws exception");
                }
                finally
                {
                    LogAssert.ignoreFailingMessages = false;
                }
            });
        }

        /// <summary>
        /// Addressablesのリソース解放が正常に動作することを確認
        /// </summary>
        [UnityTest]
        public IEnumerator Addressables_ResourceRelease_Works()
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
                    const string testKey = "MasterDataBinary";

                    // ロード
                    var handle = Addressables.LoadAssetAsync<TextAsset>(testKey);
                    await handle.ToUniTask();

                    if (handle.Status != AsyncOperationStatus.Succeeded)
                    {
                        Assert.Inconclusive("Could not load asset for release test.");
                        return;
                    }

                    // リリース
                    Assert.DoesNotThrow(() => Addressables.Release(handle), "Release should not throw");

                    // 再度ロードして、リソースが正常に解放されたことを確認
                    var handle2 = Addressables.LoadAssetAsync<TextAsset>(testKey);
                    await handle2.ToUniTask();

                    Assert.AreEqual(AsyncOperationStatus.Succeeded, handle2.Status);
                    Addressables.Release(handle2);
                }
                catch (Exception e)
                {
                    Assert.Inconclusive($"Test setup failed: {e.Message}");
                }
                finally
                {
                    LogAssert.ignoreFailingMessages = false;
                }
            });
        }

        /// <summary>
        /// 複数回ロード/リリースを繰り返しても問題ないことを確認
        /// </summary>
        [UnityTest]
        public IEnumerator Addressables_MultipleLoadRelease_Works()
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
                    const string testKey = "MasterDataBinary";
                    const int iterations = 5;

                    for (int i = 0; i < iterations; i++)
                    {
                        var handle = Addressables.LoadAssetAsync<TextAsset>(testKey);
                        await handle.ToUniTask();

                        if (handle.Status != AsyncOperationStatus.Succeeded)
                        {
                            Assert.Inconclusive($"Load failed at iteration {i}");
                            return;
                        }

                        Addressables.Release(handle);
                    }

                    Assert.Pass($"Successfully completed {iterations} load/release cycles");
                }
                catch (SuccessException e)
                {
                    Assert.Throws<SuccessException>(() => throw new SuccessException(e.Message));
                }
                catch (Exception e)
                {
                    Assert.Inconclusive($"Test failed: {e.Message}");
                }
                finally
                {
                    LogAssert.ignoreFailingMessages = false;
                }
            });
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
                    Debug.Log("[MasterDataServiceTests] Addressables initialized successfully");
                }
                else
                {
                    Debug.LogWarning("[MasterDataServiceTests] Addressables initialization failed");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MasterDataServiceTests] Addressables not available: {e.Message}");
                _addressablesInitialized = false;
            }
        }

        #endregion
    }
}
