using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.MasterData;
using Game.Library.Shared.MasterData.MemoryTables;
using Game.MVP.Survivor.Services;
using Game.Shared.Constants;
using Game.Shared.Extensions;
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
        // スポーン設定定数
        // フォールバック値: SurvivorStageWaveEnemyMaster.MinSpawnDistance/MaxSpawnDistanceが0の場合に使用
        private const float SpawnRetryDelay = 0.5f;          // スポーン失敗時の再試行間隔（秒）
        private const float DefaultMinSpawnDistance = 12f;   // フォールバック: 最小スポーン距離
        private const float DefaultMaxSpawnDistance = 18f;   // フォールバック: 最大スポーン距離
        private const int MaxSpawnAttempts = 10;             // コライダーチェックの最大試行回数
        private const float SpawnHeightOffset = 0.5f;        // コライダーチェック時の高さオフセット

        [Header("Pool Settings")]
        [SerializeField] private int _poolSizePerEnemy = 20;

        [Header("Spawn Settings")]
        [Tooltip("スポーン時の衝突チェック対象レイヤー（Structureレイヤー推奨）")]
        [SerializeField] private LayerMask _obstacleLayerMask;

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

            // レイヤーマスクが未設定の場合、Structureレイヤーを使用
            if (_obstacleLayerMask == 0)
            {
                if (LayerConstants.Structure != -1)
                {
                    _obstacleLayerMask = LayerMaskConstants.Structure;
                    Debug.Log($"[SurvivorEnemySpawner] Using 'Structure' layer for spawn collision check");
                }
                else
                {
                    // Structureレイヤーがない場合は全レイヤー（Defaultを除く）
                    _obstacleLayerMask = ~LayerMaskConstants.Default;
                    Debug.LogWarning("[SurvivorEnemySpawner] 'Structure' layer not found, using all layers except Default");
                }
            }

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

            // ウェーブ変更を購読（初期値0は無視）
            _waveManager.CurrentWave
                .Where(wave => wave > 0)
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
            if (!instance.TryGetComponent<SurvivorEnemyController>(out var controller))
            {
                Debug.LogError($"[SurvivorEnemySpawner] SurvivorEnemyController not found on prefab: {enemyId}");
                Destroy(instance);
                return null;
            }

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
            if (!_isSpawning)
            {
                return;
            }

            if (_playerTransform == null)
            {
                Debug.LogWarning("[SurvivorEnemySpawner] Update: _playerTransform is null");
                return;
            }

            if (_enemySpawnList == null || _enemySpawnList.Count == 0)
            {
                Debug.LogWarning($"[SurvivorEnemySpawner] Update: _enemySpawnList is null or empty. List={_enemySpawnList}, Count={_enemySpawnList?.Count ?? 0}");
                return;
            }

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

            // 同時存在数制限チェック（マスターデータのMaxConcurrentを参照）
            if (!CanSpawnEnemy(enemyMaster))
            {
                // 制限に達している場合はスキップして次の敵タイプを試す
                _spawnTimer = SpawnRetryDelay;
                _currentSpawnIndex++;
                return;
            }

            var enemy = GetFromPool(spawnInfo.EnemyId);
            if (enemy == null)
            {
                Debug.LogWarning($"[SurvivorEnemySpawner] Pool exhausted for enemy {spawnInfo.EnemyId}, creating new");
                enemy = CreateEnemy(spawnInfo.EnemyId);
            }

            if (enemy == null)
            {
                Debug.LogError($"[SurvivorEnemySpawner] Failed to get/create enemy {spawnInfo.EnemyId}");
                return;
            }

            // スポーン位置計算（マスターデータのSpawnRadiusでコライダーチェック）
            float minDist = spawnInfo.MinSpawnDistance > 0 ? spawnInfo.MinSpawnDistance : DefaultMinSpawnDistance;
            float maxDist = spawnInfo.MaxSpawnDistance > 0 ? spawnInfo.MaxSpawnDistance : DefaultMaxSpawnDistance;
            float spawnRadius = enemyMaster.SpawnRadius.ToUnit(); // 1000倍値から実数に変換

            if (!TryGetValidSpawnPosition(minDist, maxDist, spawnRadius, out var spawnPosition))
            {
                // 有効なスポーン位置が見つからない場合は次回に延期
                _spawnTimer = SpawnRetryDelay;
                Debug.LogWarning($"[SurvivorEnemySpawner] Could not find valid spawn position for {enemyMaster.Name}, retrying later");
                return;
            }

            enemy.transform.position = spawnPosition;
            enemy.gameObject.SetActive(true);
            Debug.Log($"[SurvivorEnemySpawner] Spawned {enemyMaster.Name} at {spawnPosition}");

            // マスターデータから初期化
            enemy.Initialize(
                enemyMaster,
                _playerTransform,
                _currentSpawnInfo.EnemySpeedMultiplier,
                _currentSpawnInfo.EnemyHealthMultiplier,
                _currentSpawnInfo.EnemyDamageMultiplier,
                _currentSpawnInfo.ExperienceMultiplier,
                spawnInfo.ItemDropGroupId,
                spawnInfo.ExpDropGroupId
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

        /// <summary>
        /// 指定した敵がスポーン可能か（同時存在数制限チェック）
        /// マスターデータのMaxConcurrentを参照（0=無制限）
        /// </summary>
        private bool CanSpawnEnemy(SurvivorEnemyMaster enemyMaster)
        {
            var maxConcurrent = enemyMaster.MaxConcurrent;

            // 0は無制限
            if (maxConcurrent <= 0)
                return true;

            // 同じ敵IDのアクティブ数をカウント
            var activeCount = GetActiveCountByEnemyId(enemyMaster.Id);
            return activeCount < maxConcurrent;
        }

        /// <summary>
        /// 指定した敵IDのアクティブ数を取得
        /// </summary>
        private int GetActiveCountByEnemyId(int enemyId)
        {
            int count = 0;
            foreach (var enemy in _activeEnemies)
            {
                if (enemy != null && !enemy.IsDead && enemy.EnemyId == enemyId)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// 有効なスポーン位置を取得（コライダーチェック付き）
        /// </summary>
        /// <param name="minDistance">最小距離</param>
        /// <param name="maxDistance">最大距離</param>
        /// <param name="spawnRadius">敵の衝突判定半径</param>
        /// <param name="position">有効なスポーン位置（成功時）</param>
        /// <returns>有効な位置が見つかった場合true</returns>
        private bool TryGetValidSpawnPosition(float minDistance, float maxDistance, float spawnRadius, out Vector3 position)
        {
            for (int attempt = 0; attempt < MaxSpawnAttempts; attempt++)
            {
                var candidatePosition = GetRandomSpawnPosition(minDistance, maxDistance);

                // スポーン位置が有効かチェック
                if (IsValidSpawnPosition(candidatePosition, spawnRadius))
                {
                    position = candidatePosition;
                    return true;
                }
            }

            // 全ての試行が失敗した場合、コライダーチェックなしで位置を返す（フォールバック）
            position = GetRandomSpawnPosition(minDistance, maxDistance);
            return true; // フォールバックとして常に成功扱い
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

        /// <summary>
        /// スポーン位置が有効かチェック（構造物との衝突判定）
        /// </summary>
        /// <param name="position">チェックする位置</param>
        /// <param name="radius">敵の衝突判定半径</param>
        /// <returns>有効な場合true</returns>
        private bool IsValidSpawnPosition(Vector3 position, float radius)
        {
            // 地面より少し上からチェック（敵の中心位置）
            var checkPosition = position + Vector3.up * SpawnHeightOffset;

            // 指定半径の球でコライダーチェック（障害物がなければtrue）
            // QueryTriggerInteraction.Ignoreでトリガーコライダーは無視
            return !Physics.CheckSphere(checkPosition, radius, _obstacleLayerMask, QueryTriggerInteraction.Ignore);
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

            // 死亡アニメーション再生後にプールに戻す（マスターデータから時間を取得）
            var deathDelay = enemy.DeathAnimDuration;
            Observable.Timer(TimeSpan.FromSeconds(deathDelay))
                .Subscribe(_ => ReturnToPool(enemy))
                .AddTo(this);

            // ウェーブサービスに通知（ボスかどうかも伝える）
            _waveManager.OnEnemyKilled(enemy.IsBoss);
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

            // プール内の敵を破棄
            foreach (var pool in _pools.Values)
            {
                while (pool.Count > 0)
                {
                    var enemy = pool.Dequeue();
                    if (enemy != null)
                    {
                        Destroy(enemy.gameObject);
                    }
                }
            }
            _pools.Clear();

            // アクティブな敵を破棄
            foreach (var enemy in _activeEnemies)
            {
                if (enemy != null)
                {
                    Destroy(enemy.gameObject);
                }
            }
            _activeEnemies.Clear();

            // ロードしたプレハブをリリース
            foreach (var prefab in _enemyPrefabs.Values)
            {
                _assetService.ReleaseAsset(prefab);
            }
            _enemyPrefabs.Clear();
        }
    }
}