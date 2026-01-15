using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.MasterData;
using Game.MVP.Core.Services;
using Game.MVP.Survivor.Services;
using Game.Shared.Services;
using R3;
using UnityEngine;
using VContainer;
using Random = UnityEngine.Random;

namespace Game.MVP.Survivor.Enemy
{
    /// <summary>
    /// Survivor敵スポーナー
    /// マスターデータに基づいて敵を生成・管理
    /// </summary>
    public class SurvivorEnemySpawner : MonoBehaviour
    {
        [Header("Pool Settings")]
        [SerializeField] private int _poolSizePerEnemy = 20;

        [Header("References")]
        [SerializeField] private Transform _playerTransform;

        // DI
        [Inject] private IAddressableAssetService _assetService;
        [Inject] private IMasterDataService _masterDataService;
        private MemoryDatabase MemoryDatabase => _masterDataService.MemoryDatabase;

        // Pool（敵IDごとにプール管理）
        private readonly Dictionary<int, Queue<SurvivorEnemyController>> _pools = new();
        private readonly Dictionary<int, GameObject> _enemyPrefabs = new();
        private readonly List<SurvivorEnemyController> _activeEnemies = new();

        // Services
        private SurvivorStageWaveManager _waveManager;

        // State
        private bool _isSpawning;
        private WaveSpawnInfo _currentSpawnInfo;
        private List<WaveEnemySpawnInfo> _enemySpawnList;
        private int _currentSpawnIndex;
        private float _spawnTimer;
        private int _remainingSpawnCount;

        // Events
        private readonly Subject<SurvivorEnemyController> _onEnemyKilled = new();
        public Observable<SurvivorEnemyController> OnEnemyKilled => _onEnemyKilled;

        public void SetPlayer(Transform player)
        {
            _playerTransform = player;
        }

        public async UniTask InitializeAsync(SurvivorStageWaveManager waveManager)
        {
            _waveManager = waveManager;

            // 全ての敵タイプのプレハブを事前読み込み
            var allEnemies = MemoryDatabase.SurvivorEnemyMasterTable.All;
            foreach (var enemy in allEnemies)
            {
                if (!_enemyPrefabs.ContainsKey(enemy.Id))
                {
                    var prefab = await _assetService.LoadAssetAsync<GameObject>(enemy.AssetName);
                    _enemyPrefabs[enemy.Id] = prefab;

                    // プール初期化
                    _pools[enemy.Id] = new Queue<SurvivorEnemyController>();
                    for (int i = 0; i < _poolSizePerEnemy; i++)
                    {
                        var controller = CreateEnemy(enemy.Id);
                        controller.gameObject.SetActive(false);
                        _pools[enemy.Id].Enqueue(controller);
                    }
                }
            }

            // ウェーブ変更を購読
            _waveManager.CurrentWave
                .Skip(1) // 初期値をスキップ
                .Subscribe(_ => OnWaveChanged())
                .AddTo(this);

            Debug.Log($"[SurvivorEnemySpawner] Initialized with {_enemyPrefabs.Count} enemy types");
        }

        private SurvivorEnemyController CreateEnemy(int enemyId)
        {
            if (!_enemyPrefabs.TryGetValue(enemyId, out var prefab))
            {
                Debug.LogError($"[SurvivorEnemySpawner] Prefab not found for enemy ID: {enemyId}");
                return null;
            }

            var instance = Instantiate(prefab, transform);
            var controller = instance.GetComponent<SurvivorEnemyController>();

            controller.OnDeath
                .Subscribe(OnEnemyDeath)
                .AddTo(this);

            return controller;
        }

        private void OnWaveChanged()
        {
            _currentSpawnInfo = _waveManager.GetSpawnInfo();
            _enemySpawnList = new List<WaveEnemySpawnInfo>(_waveManager.GetEnemySpawnList());
            _currentSpawnIndex = 0;
            _spawnTimer = 0f;
            _remainingSpawnCount = _currentSpawnInfo.EnemyCount;
            _isSpawning = true;

            Debug.Log($"[SurvivorEnemySpawner] Wave started. Enemy types: {_enemySpawnList.Count}, Total: {_remainingSpawnCount}");
        }

        private void Update()
        {
            if (!_isSpawning || _playerTransform == null || _enemySpawnList == null || _enemySpawnList.Count == 0)
                return;

            _spawnTimer -= Time.deltaTime;

            if (_spawnTimer <= 0f && _remainingSpawnCount > 0)
            {
                SpawnNextEnemy();
            }
        }

        private void SpawnNextEnemy()
        {
            if (_currentSpawnIndex >= _enemySpawnList.Count)
            {
                _currentSpawnIndex = 0; // ループ
            }

            var spawnInfo = _enemySpawnList[_currentSpawnIndex];

            // 敵マスターデータ取得
            if (!MemoryDatabase.SurvivorEnemyMasterTable.TryFindById(spawnInfo.EnemyId, out var enemyMaster))
            {
                Debug.LogError($"[SurvivorEnemySpawner] Enemy master not found: {spawnInfo.EnemyId}");
                return;
            }

            var enemy = GetFromPool(spawnInfo.EnemyId);
            if (enemy == null)
            {
                Debug.LogWarning($"[SurvivorEnemySpawner] Pool exhausted for enemy {spawnInfo.EnemyId}, creating new");
                enemy = CreateEnemy(spawnInfo.EnemyId);
            }

            if (enemy == null) return;

            // スポーン位置計算
            float minDist = spawnInfo.MinSpawnDistance > 0 ? spawnInfo.MinSpawnDistance : 10f;
            float maxDist = spawnInfo.MaxSpawnDistance > 0 ? spawnInfo.MaxSpawnDistance : 15f;
            Vector3 spawnPosition = GetRandomSpawnPosition(minDist, maxDist);

            enemy.transform.position = spawnPosition;
            enemy.gameObject.SetActive(true);

            // マスターデータから初期化
            enemy.Initialize(
                enemyMaster,
                _playerTransform,
                _currentSpawnInfo.EnemySpeedMultiplier,
                _currentSpawnInfo.EnemyHealthMultiplier,
                _currentSpawnInfo.EnemyDamageMultiplier,
                _currentSpawnInfo.ExperienceMultiplier
            );

            _activeEnemies.Add(enemy);
            _remainingSpawnCount--;
            _spawnTimer = spawnInfo.SpawnInterval;
            _currentSpawnIndex++;

            if (_remainingSpawnCount <= 0)
            {
                _isSpawning = false;
            }
        }

        private Vector3 GetRandomSpawnPosition(float minDistance, float maxDistance)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float distance = Random.Range(minDistance, maxDistance);

            Vector3 offset = new Vector3(
                Mathf.Cos(angle) * distance,
                0f,
                Mathf.Sin(angle) * distance
            );

            return _playerTransform.position + offset;
        }

        private SurvivorEnemyController GetFromPool(int enemyId)
        {
            if (!_pools.TryGetValue(enemyId, out var pool))
                return null;

            while (pool.Count > 0)
            {
                var enemy = pool.Dequeue();
                if (enemy != null)
                {
                    return enemy;
                }
            }

            return null;
        }

        private void ReturnToPool(SurvivorEnemyController enemy)
        {
            var enemyId = enemy.EnemyId;
            enemy.ResetForPool();

            if (_pools.TryGetValue(enemyId, out var pool))
            {
                pool.Enqueue(enemy);
            }
        }

        private void OnEnemyDeath(SurvivorEnemyController enemy)
        {
            _activeEnemies.Remove(enemy);
            _onEnemyKilled.OnNext(enemy);

            // 少し待ってからプールに戻す（死亡アニメーション再生のため）
            Observable.Timer(TimeSpan.FromSeconds(1f))
                .Subscribe(_ => ReturnToPool(enemy))
                .AddTo(this);

            // ウェーブサービスに通知
            _waveManager.OnEnemyKilled();
        }

        /// <summary>
        /// 全ての敵をクリア
        /// </summary>
        public void ClearAllEnemies()
        {
            foreach (var enemy in _activeEnemies.ToArray())
            {
                ReturnToPool(enemy);
            }

            _activeEnemies.Clear();
            _isSpawning = false;
        }

        private void OnDestroy()
        {
            _onEnemyKilled.Dispose();
        }
    }
}
