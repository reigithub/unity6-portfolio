using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// 敵スポーン機能のPlayModeテスト
    /// オブジェクトプール、スポーン位置、スポーン制御をテスト
    /// </summary>
    [TestFixture]
    public class EnemySpawnerTests
    {
        private GameObject _spawnerObject;
        private TestEnemySpawner _spawner;
        private List<GameObject> _spawnedEnemies;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // スポーナーオブジェクトを作成
            _spawnerObject = new GameObject("TestEnemySpawner");
            _spawner = _spawnerObject.AddComponent<TestEnemySpawner>();
            _spawnedEnemies = new List<GameObject>();

            // 敵プレハブの代わりにシンプルなGameObjectを使用
            _spawner.EnemyPrefab = CreateTestEnemyPrefab();

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // スポーンされた敵をクリーンアップ
            foreach (var enemy in _spawnedEnemies)
            {
                if (enemy != null)
                {
                    Object.Destroy(enemy);
                }
            }
            _spawnedEnemies.Clear();

            if (_spawnerObject != null)
            {
                Object.Destroy(_spawnerObject);
            }

            yield return null;
        }

        /// <summary>
        /// 敵のスポーンが正常に動作することを確認
        /// </summary>
        [UnityTest]
        public IEnumerator SpawnEnemy_CreatesNewEnemy()
        {
            // Act
            var enemy = _spawner.SpawnEnemy(Vector3.zero);
            _spawnedEnemies.Add(enemy);
            yield return null;

            // Assert
            Assert.IsNotNull(enemy, "Spawned enemy should not be null");
            Assert.IsTrue(enemy.activeInHierarchy, "Spawned enemy should be active");
        }

        /// <summary>
        /// 指定位置にスポーンされることを確認
        /// </summary>
        [UnityTest]
        public IEnumerator SpawnEnemy_AtSpecifiedPosition_CorrectPosition()
        {
            // Arrange
            var spawnPosition = new Vector3(5f, 0f, 10f);

            // Act
            var enemy = _spawner.SpawnEnemy(spawnPosition);
            _spawnedEnemies.Add(enemy);
            yield return null;

            // Assert
            Assert.AreEqual(spawnPosition, enemy.transform.position, "Enemy should spawn at specified position");
        }

        /// <summary>
        /// 複数の敵をスポーンできることを確認
        /// </summary>
        [UnityTest]
        public IEnumerator SpawnMultipleEnemies_AllCreated()
        {
            // Arrange
            int spawnCount = 10;

            // Act
            for (int i = 0; i < spawnCount; i++)
            {
                var enemy = _spawner.SpawnEnemy(new Vector3(i, 0, 0));
                _spawnedEnemies.Add(enemy);
            }
            yield return null;

            // Assert
            Assert.AreEqual(spawnCount, _spawnedEnemies.Count);
            foreach (var enemy in _spawnedEnemies)
            {
                Assert.IsNotNull(enemy);
                Assert.IsTrue(enemy.activeInHierarchy);
            }
        }

        /// <summary>
        /// オブジェクトプールが正しく動作することを確認
        /// </summary>
        [UnityTest]
        public IEnumerator ObjectPool_ReusesDeactivatedEnemies()
        {
            // Arrange - 敵をスポーンして非アクティブ化
            var enemy1 = _spawner.SpawnEnemy(Vector3.zero);
            _spawnedEnemies.Add(enemy1);
            yield return null;

            // Act - 非アクティブ化（プールに戻す）
            _spawner.ReturnToPool(enemy1);
            yield return null;

            // Assert - 非アクティブ
            Assert.IsFalse(enemy1.activeInHierarchy, "Returned enemy should be inactive");

            // Act - 再度スポーン（プールから取得）
            var enemy2 = _spawner.SpawnEnemy(Vector3.one);
            yield return null;

            // Assert - 同じインスタンスが再利用される
            Assert.AreSame(enemy1, enemy2, "Pool should reuse the same enemy instance");
            Assert.IsTrue(enemy2.activeInHierarchy, "Reused enemy should be active");
        }

        /// <summary>
        /// スポーン範囲内のランダム位置が正しいことを確認
        /// </summary>
        [UnityTest]
        public IEnumerator RandomSpawnPosition_WithinBounds()
        {
            // Arrange
            float minRange = -10f;
            float maxRange = 10f;
            int testCount = 100;

            // Act & Assert
            for (int i = 0; i < testCount; i++)
            {
                var position = _spawner.GetRandomSpawnPosition(minRange, maxRange);

                Assert.GreaterOrEqual(position.x, minRange, $"X position {position.x} should be >= {minRange}");
                Assert.LessOrEqual(position.x, maxRange, $"X position {position.x} should be <= {maxRange}");
                Assert.GreaterOrEqual(position.z, minRange, $"Z position {position.z} should be >= {minRange}");
                Assert.LessOrEqual(position.z, maxRange, $"Z position {position.z} should be <= {maxRange}");
            }
            yield return null;
        }

        /// <summary>
        /// スポーン間隔が正しく機能することを確認
        /// </summary>
        [UnityTest]
        public IEnumerator SpawnInterval_RespectsDelay()
        {
            // Arrange
            float spawnInterval = 0.5f;
            _spawner.SpawnInterval = spawnInterval;
            int expectedSpawns = 3;

            // Act - 自動スポーンを開始
            _spawner.StartAutoSpawn();
            yield return new WaitForSeconds(spawnInterval * expectedSpawns + 0.1f);
            _spawner.StopAutoSpawn();

            // Assert - 期待数の敵がスポーンされている
            int actualSpawns = _spawner.SpawnedCount;
            Assert.GreaterOrEqual(actualSpawns, expectedSpawns - 1, "Should spawn approximately expected number of enemies");
            Assert.LessOrEqual(actualSpawns, expectedSpawns + 1, "Should not spawn significantly more than expected");
        }

        /// <summary>
        /// 最大スポーン数の制限が機能することを確認
        /// </summary>
        [UnityTest]
        public IEnumerator MaxSpawnLimit_RespectsLimit()
        {
            // Arrange
            int maxEnemies = 5;
            _spawner.MaxEnemies = maxEnemies;

            // Act
            for (int i = 0; i < 10; i++)
            {
                var enemy = _spawner.SpawnEnemyWithLimit(Vector3.zero);
                if (enemy != null)
                {
                    _spawnedEnemies.Add(enemy);
                }
            }
            yield return null;

            // Assert
            Assert.AreEqual(maxEnemies, _spawnedEnemies.Count, "Should not exceed max enemy limit");
        }

        /// <summary>
        /// 敵の破棄後にカウントが更新されることを確認
        /// </summary>
        [UnityTest]
        public IEnumerator EnemyDestroyed_UpdatesCount()
        {
            // Arrange
            _spawner.MaxEnemies = 3;
            for (int i = 0; i < 3; i++)
            {
                var enemy = _spawner.SpawnEnemyWithLimit(Vector3.zero);
                _spawnedEnemies.Add(enemy);
            }
            yield return null;

            Assert.AreEqual(3, _spawner.ActiveEnemyCount);

            // Act - 1体をプールに戻す
            var enemyToReturn = _spawnedEnemies[0];
            _spawner.ReturnToPool(enemyToReturn);
            yield return null;

            // Assert
            Assert.AreEqual(2, _spawner.ActiveEnemyCount, "Active count should decrease after return to pool");

            // Act - 新しい敵をスポーンできる
            var newEnemy = _spawner.SpawnEnemyWithLimit(Vector3.one);
            Assert.IsNotNull(newEnemy, "Should be able to spawn after returning enemy to pool");
        }

        #region Helper Methods and Classes

        private GameObject CreateTestEnemyPrefab()
        {
            var prefab = new GameObject("TestEnemyPrefab");
            prefab.AddComponent<BoxCollider>();
            prefab.SetActive(false);
            return prefab;
        }

        /// <summary>
        /// テスト用の簡易スポーナー
        /// </summary>
        private class TestEnemySpawner : MonoBehaviour
        {
            public GameObject EnemyPrefab { get; set; }
            public float SpawnInterval { get; set; } = 1f;
            public int MaxEnemies { get; set; } = 100;
            public int SpawnedCount { get; private set; }
            public int ActiveEnemyCount => _activeEnemies.Count;

            private readonly Queue<GameObject> _pool = new Queue<GameObject>();
            private readonly List<GameObject> _activeEnemies = new List<GameObject>();
            private bool _autoSpawning;
            private Coroutine _autoSpawnCoroutine;

            public GameObject SpawnEnemy(Vector3 position)
            {
                GameObject enemy;

                if (_pool.Count > 0)
                {
                    enemy = _pool.Dequeue();
                    enemy.transform.position = position;
                    enemy.SetActive(true);
                }
                else
                {
                    enemy = Instantiate(EnemyPrefab, position, Quaternion.identity);
                    enemy.SetActive(true);
                }

                _activeEnemies.Add(enemy);
                SpawnedCount++;
                return enemy;
            }

            public GameObject SpawnEnemyWithLimit(Vector3 position)
            {
                if (_activeEnemies.Count >= MaxEnemies)
                {
                    return null;
                }
                return SpawnEnemy(position);
            }

            public void ReturnToPool(GameObject enemy)
            {
                enemy.SetActive(false);
                _activeEnemies.Remove(enemy);
                _pool.Enqueue(enemy);
            }

            public Vector3 GetRandomSpawnPosition(float minRange, float maxRange)
            {
                return new Vector3(
                    Random.Range(minRange, maxRange),
                    0f,
                    Random.Range(minRange, maxRange)
                );
            }

            public void StartAutoSpawn()
            {
                _autoSpawning = true;
                SpawnedCount = 0;
                _autoSpawnCoroutine = StartCoroutine(AutoSpawnCoroutine());
            }

            public void StopAutoSpawn()
            {
                _autoSpawning = false;
                if (_autoSpawnCoroutine != null)
                {
                    StopCoroutine(_autoSpawnCoroutine);
                }
            }

            private IEnumerator AutoSpawnCoroutine()
            {
                while (_autoSpawning)
                {
                    var enemy = SpawnEnemy(GetRandomSpawnPosition(-5f, 5f));
                    yield return new WaitForSeconds(SpawnInterval);
                }
            }
        }

        #endregion
    }
}
