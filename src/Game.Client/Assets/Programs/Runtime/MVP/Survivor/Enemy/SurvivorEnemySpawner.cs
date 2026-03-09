using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Client.MasterData;
using Game.Library.Shared.Dto;
using Game.MVP.Survivor.Services;
using Game.Shared.Constants;
using Game.Shared.Extensions;
using Game.Shared.Network;
using Game.Shared.Network.Survivor;
using Game.Shared.Playmode;
using Game.Shared.Services;
using R3;
using Unity.Profiling;
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
        // Profiler markers
        private static readonly ProfilerMarker s_spawnEnemyMarker = new("ProfilerMarker.Spawn.Enemy");
        private static readonly ProfilerMarker s_validatePositionMarker = new("ProfilerMarker.Spawn.ValidatePosition");
        private static readonly ProfilerMarker s_getFromPoolMarker = new("ProfilerMarker.Pool.GetEnemy");
        private static readonly ProfilerMarker s_returnToPoolMarker = new("ProfilerMarker.Pool.ReturnEnemy");

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

        // マルチプレイ: 複数プレイヤーの Transform リスト
        private readonly List<Transform> _playerTransforms = new();

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

        private bool _isClient;

        // ネットワーク敵同期
        private ISurvivorNetworkBridge _networkBridge;
        private const float EnemySyncInterval = 1.0f; // 1Hz
        private float _enemySyncTimer;
        private int _nextNetworkId;
        private readonly Dictionary<SurvivorEnemyController, int> _enemyNetworkIds = new();

        // Events
        private readonly Subject<SurvivorEnemyController> _onEnemyKilled = new();
        public Observable<SurvivorEnemyController> OnEnemyKilled => _onEnemyKilled;

        public void SetPlayer(Transform player)
        {
            _playerTransform = player;
            if (!_playerTransforms.Contains(player))
                _playerTransforms.Add(player);
        }

        public void AddPlayer(Transform player)
        {
            if (player != null && !_playerTransforms.Contains(player))
            {
                _playerTransforms.Add(player);
                // SP 後方互換: _playerTransform が未設定なら設定
                if (_playerTransform == null)
                    _playerTransform = player;
            }
        }

        public void RemovePlayer(Transform player)
        {
            _playerTransforms.Remove(player);
            if (_playerTransform == player)
                _playerTransform = _playerTransforms.Count > 0 ? _playerTransforms[0] : null;
        }

        /// <summary>
        /// ランダムにプレイヤーの Transform を選択する。
        /// </summary>
        private Transform GetRandomPlayerTransform()
        {
            // null エントリを除外
            for (int i = _playerTransforms.Count - 1; i >= 0; i--)
            {
                if (_playerTransforms[i] == null)
                    _playerTransforms.RemoveAt(i);
            }

            if (_playerTransforms.Count > 0)
                return _playerTransforms[Random.Range(0, _playerTransforms.Count)];
            return _playerTransform;
        }

        public void SetNetworkBridge(ISurvivorNetworkBridge bridge)
        {
            _networkBridge = bridge;
        }

        public async UniTask InitializeAsync(SurvivorStageWaveManager waveManager)
        {
            _waveManager = waveManager;
            _isClient = NetworkModeHelper.IsNetworkClient;

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

            // MP Client: 敵はサーバーバッチ同期で表示、ローカルスポーン不要
            if (!_isClient)
            {
                _waveManager.CurrentWave
                    .Where(wave => wave > 0)
                    .Subscribe(_ => OnWaveChanged())
                    .AddTo(this);
            }

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

            if (UnityPlaymodeHelper.IsClient())
            {
                instance.AddComponent<SurvivorEnemyPresenter>();
            }

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
            // サーバー: 定期的に敵状態をバッチ送信
            if (_networkBridge != null)
            {
                _enemySyncTimer -= Time.deltaTime;
                if (_enemySyncTimer <= 0f)
                {
                    _enemySyncTimer = EnemySyncInterval;
                    SyncEnemyStatesToNetwork();
                }
            }

            // MP Client: ローカルスポーン無効
            if (_isClient) return;

            if (!_isSpawning)
            {
                return;
            }

            if (GetRandomPlayerTransform() == null)
            {
                Debug.LogWarning("[SurvivorEnemySpawner] Update: No player transform available");
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

        private void SyncEnemyStatesToNetwork()
        {
            if (_activeEnemies.Count == 0)
                return;

            var snapshots = new SurvivorNetworkEnemyStateSnapshot[_activeEnemies.Count];
            for (int i = 0; i < _activeEnemies.Count; i++)
            {
                var enemy = _activeEnemies[i];
                var networkId = _enemyNetworkIds.TryGetValue(enemy, out var id) ? id : -1;
                snapshots[i] = new SurvivorNetworkEnemyStateSnapshot
                {
                    NetworkId = networkId,
                    EnemyMasterId = enemy.EnemyId,
                    PositionX = enemy.transform.position.x,
                    PositionZ = enemy.transform.position.z,
                    CurrentHp = enemy.CurrentHp,
                    SyncType = EnemySyncType.PositionUpdate
                };
            }
            _networkBridge.BroadcastEnemyStates(snapshots);
        }

        private void SpawnNextEnemy()
        {
            using (s_spawnEnemyMarker.Auto())
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

                // マスターデータから初期化（MP: ランダムプレイヤーをターゲット）
                var targetPlayer = GetRandomPlayerTransform();
                enemy.Initialize(
                    enemyMaster,
                    targetPlayer,
                    _currentSpawnInfo.EnemySpeedMultiplier,
                    _currentSpawnInfo.EnemyHealthMultiplier,
                    _currentSpawnInfo.EnemyDamageMultiplier,
                    _currentSpawnInfo.ExperienceMultiplier,
                    spawnInfo.ItemDropGroupId,
                    spawnInfo.ExpDropGroupId
                );

                if (UnityPlaymodeHelper.IsClient())
                {
                    if (enemy.TryGetComponent<SurvivorEnemyPresenter>(out var component))
                    {
                        component.Initialize(enemy);
                    }
                }

                var networkId = _nextNetworkId++;
                _enemyNetworkIds[enemy] = networkId;
                _activeEnemies.Add(enemy);
                _remainingSpawnCount--;
                _spawnTimer = spawnInfo.SpawnInterval;
                _currentSpawnIndex++;

                // サーバー: スポーンイベントを送信
                if (_networkBridge != null)
                {
                    var spawnSnapshot = new SurvivorNetworkEnemyStateSnapshot
                    {
                        NetworkId = networkId,
                        EnemyMasterId = enemy.EnemyId,
                        PositionX = spawnPosition.x,
                        PositionZ = spawnPosition.z,
                        CurrentHp = enemy.CurrentHp,
                        SyncType = EnemySyncType.Spawn
                    };
                    _networkBridge.BroadcastEnemyStates(new[] { spawnSnapshot });
                }

                if (_remainingSpawnCount <= 0)
                {
                    _isSpawning = false;
                }
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
            using (s_validatePositionMarker.Auto())
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

            // MP: ランダムなプレイヤーの周囲にスポーン
            var target = GetRandomPlayerTransform();
            return (target != null ? target.position : Vector3.zero) + offset;
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
            using (s_getFromPoolMarker.Auto())
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
        }

        private void ReturnToPool(SurvivorEnemyController enemy)
        {
            using (s_returnToPoolMarker.Auto())
            {
                var enemyId = enemy.EnemyId;
                if (UnityPlaymodeHelper.IsClient())
                {
                    if (enemy.TryGetComponent<SurvivorEnemyPresenter>(out var component))
                    {
                        component.ResetForPool();
                    }
                }
                enemy.ResetForPool();

                if (_pools.TryGetValue(enemyId, out var pool))
                {
                    pool.Enqueue(enemy);
                }
            }
        }

        private void OnEnemyDeath(SurvivorEnemyController enemy)
        {
            Debug.Log($"[SurvivorEnemySpawner] EnemyDeath: id={enemy.EnemyId}, boss={enemy.IsBoss}, active={_activeEnemies.Count - 1}, time={Time.time:F1}s");

            // サーバー: 死亡イベントを送信
            if (_networkBridge != null && _enemyNetworkIds.TryGetValue(enemy, out var networkId))
            {
                var deathSnapshot = new SurvivorNetworkEnemyStateSnapshot
                {
                    NetworkId = networkId,
                    EnemyMasterId = enemy.EnemyId,
                    PositionX = enemy.transform.position.x,
                    PositionZ = enemy.transform.position.z,
                    CurrentHp = 0,
                    SyncType = EnemySyncType.Death
                };
                _networkBridge.BroadcastEnemyStates(new[] { deathSnapshot });
            }

            _enemyNetworkIds.Remove(enemy);
            _activeEnemies.Remove(enemy);
            _onEnemyKilled.OnNext(enemy);

            // 死亡アニメーション再生後にプールに戻す（マスターデータから時間を取得）
            var deathDelay = enemy.DeathAnimDuration;
            Observable.Timer(TimeSpan.FromSeconds(deathDelay))
                .Subscribe(_ => ReturnToPool(enemy))
                .AddTo(this);

            // ウェーブサービスに通知（ボスかどうかも伝える）
            if (!_isClient)
            {
                _waveManager.OnEnemyKilled(enemy.IsBoss);
            }
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
            _enemyNetworkIds.Clear();
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
