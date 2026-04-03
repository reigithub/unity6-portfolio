using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Client.MasterData;
using Game.Library.Shared.Dto;
using Game.MVP.Survivor.Services;
using Game.Shared.Constants;
using Game.Shared.Extensions;
using Game.Shared.Network.Fusion;
using Game.Shared.Network.Survivor;
using Game.Shared.Playmode;
using Game.Shared.Services;
using R3;
using Unity.Profiling;
using UnityEngine;
using VContainer;

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
        [Inject] private IFusionRunnerService _runnerService;
        private MemoryDatabase MemoryDatabase => _masterDataService.MemoryDatabase;

        // Pool（敵IDごとにプール管理）
        private readonly Dictionary<int, Queue<SurvivorEnemyController>> _pools = new();
        private readonly Dictionary<int, GameObject> _enemyPrefabs = new();
        private readonly List<SurvivorEnemyController> _activeEnemies = new();
        private readonly Dictionary<int, int> _activeCountByEnemyId = new();

        // Services
        private SurvivorStageWaveManager _waveManager;

        /// <summary>
        /// Wave単位のシードRNG。Wave開始時にステージID＋Wave番号からシードを生成。
        /// デバッグ再現性の確保と決定論的スポーン位置を実現する。
        /// </summary>
        private System.Random _waveRng;

        // State
        private bool _isSpawning;
        private bool _wasPaused;
        private SurvivorFusionGameState _gameState;
        private WaveSpawnInfo _currentSpawnInfo;
        private List<WaveEnemySpawnInfo> _enemySpawnList;
        private int _currentSpawnIndex;
        private float _spawnTimer;
        private int _remainingSpawnCount;

        // ネットワーク敵同期
        private const float EnemySyncInterval = 0.1f; // 10Hz
        private float _enemySyncTimer;
        private int _nextNetworkId;
        private readonly Dictionary<SurvivorEnemyController, int> _enemyNetworkIds = new();
        private readonly Dictionary<int, SurvivorEnemyController> _enemyByNetworkId = new();
        private readonly HashSet<int> _spawnedNetworkIds = new(); // クライアントに Spawn 済みの NetworkId
        private readonly List<SurvivorNetworkEnemyStateSnapshot> _pendingDeaths = new(); // 次回同期で送信する Death

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
                return _playerTransforms[NextRange(0, _playerTransforms.Count)];
            return _playerTransform;
        }

        public async UniTask InitializeAsync(SurvivorStageWaveManager waveManager)
        {
            _waveManager = waveManager;
            _runnerService.TryGet(out _gameState);

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
            if (_runnerService.IsServer)
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

            controller.OnSilentRemoval
                .Subscribe(OnEnemySilentRemoval)
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

            // Wave単位の決定論的RNGを初期化（同一ステージ・同一Waveで再現可能）
            var seed = _waveManager.StageId * 10000 + _currentSpawnInfo.WaveNumber;
            _waveRng = new System.Random(seed);

            Debug.Log($"[SurvivorEnemySpawner] Wave started. Enemy types: {_enemySpawnList.Count}, Total: {_remainingSpawnCount}, RNG Seed: {seed}");
        }

        private float GetNetworkDeltaTime()
        {
            if (_runnerService.IsActive && _runnerService.Runner != null)
                return _runnerService.Runner.DeltaTime;
            return Time.deltaTime;
        }

        private void Update()
        {
            // ポーズ状態の同期
            bool isPaused = _gameState != null && _gameState.IsPaused;
            if (isPaused != _wasPaused)
            {
                _wasPaused = isPaused;
                SetAllEnemiesPaused(isPaused);
            }

            // サーバー: 定期的に敵状態をバッチ送信（ポーズ中も位置同期は維持）
            if (_runnerService.TryGet<SurvivorFusionEnemyBatchSync>(out var batchSync))
            {
                _enemySyncTimer -= GetNetworkDeltaTime();
                if (_enemySyncTimer <= 0f)
                {
                    _enemySyncTimer = EnemySyncInterval;
                    SyncEnemyStatesToNetwork(batchSync);
                }
            }

            // MP Client: ローカルスポーン無効
            if (!_runnerService.IsServer) return;

            // ポーズ中はスポーン停止
            if (isPaused) return;

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

            _spawnTimer -= GetNetworkDeltaTime();

            if (_spawnTimer <= 0f && _remainingSpawnCount > 0)
            {
                SpawnNextEnemy();
            }
        }

        private void SyncEnemyStatesToNetwork(SurvivorFusionEnemyBatchSync batchSync)
        {
            if (_activeEnemies.Count == 0 && _pendingDeaths.Count == 0)
                return;

            var snapshots = new SurvivorNetworkEnemyStateSnapshot[_activeEnemies.Count + _pendingDeaths.Count];
            for (int i = 0; i < _activeEnemies.Count; i++)
            {
                var enemy = _activeEnemies[i];
                var networkId = _enemyNetworkIds.TryGetValue(enemy, out var id) ? id : -1;

                // 未送信のエネミーは Spawn タイプで送信（クライアントがプロキシを生成するため）
                EnemySyncType syncType;
                if (!_spawnedNetworkIds.Contains(networkId))
                {
                    syncType = EnemySyncType.Spawn;
                    _spawnedNetworkIds.Add(networkId);
                }
                else if (enemy.CurrentAnimationState == EnemyAnimationState.Attack)
                {
                    syncType = EnemySyncType.Attack;
                }
                else
                {
                    syncType = EnemySyncType.PositionUpdate;
                }

                snapshots[i] = new SurvivorNetworkEnemyStateSnapshot
                {
                    NetworkId = networkId,
                    EnemyMasterId = enemy.EnemyId,
                    PositionX = enemy.transform.position.x,
                    PositionY = enemy.transform.position.y,
                    PositionZ = enemy.transform.position.z,
                    VelocityX = enemy.Velocity.x,
                    VelocityY = enemy.Velocity.y,
                    VelocityZ = enemy.Velocity.z,
                    CurrentHp = enemy.CurrentHp,
                    SyncType = syncType
                };
            }

            // 保留中の Death を末尾に追加
            for (int i = 0; i < _pendingDeaths.Count; i++)
            {
                snapshots[_activeEnemies.Count + i] = _pendingDeaths[i];
            }
            _pendingDeaths.Clear();

            batchSync.WriteEnemyStates(snapshots);
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
                    _runnerService,
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
                _enemyByNetworkId[networkId] = enemy;
                enemy.SetNetworkId(networkId);
                _activeEnemies.Add(enemy);
                IncrementActiveCount(enemy.EnemyId);
                _remainingSpawnCount--;
                _spawnTimer = spawnInfo.SpawnInterval;
                _currentSpawnIndex++;

                // Spawn SyncType は SyncEnemyStatesToNetwork で _spawnedNetworkIds により自動設定される
                // 個別の WriteEnemyStates は ActiveCount をリセットするため使用しない

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
            return _activeCountByEnemyId.TryGetValue(enemyId, out var count) ? count : 0;
        }

        private void IncrementActiveCount(int enemyId)
        {
            _activeCountByEnemyId.TryGetValue(enemyId, out var count);
            _activeCountByEnemyId[enemyId] = count + 1;
        }

        private void DecrementActiveCount(int enemyId)
        {
            if (_activeCountByEnemyId.TryGetValue(enemyId, out var count) && count > 0)
                _activeCountByEnemyId[enemyId] = count - 1;
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
                    if (!TryGetRandomSpawnPosition(minDistance, maxDistance, out var candidatePosition))
                        continue; // NavMesh 上に位置が見つからない場合はリトライ

                    if (IsValidSpawnPosition(candidatePosition, spawnRadius))
                    {
                        position = candidatePosition;
                        return true;
                    }
                }

                // フォールバック: コライダーチェックなしだが NavMesh 上の位置のみ許可
                for (int attempt = 0; attempt < MaxSpawnAttempts; attempt++)
                {
                    if (TryGetRandomSpawnPosition(minDistance, maxDistance, out position))
                        return true;
                }

                position = default;
                return false;
            }
        }

        private float NextRange(float min, float max)
        {
            if (_waveRng == null)
                return UnityEngine.Random.Range(min, max);
            return (float)(_waveRng.NextDouble() * (max - min) + min);
        }

        private int NextRange(int min, int maxExclusive)
        {
            if (_waveRng == null)
                return UnityEngine.Random.Range(min, maxExclusive);
            return _waveRng.Next(min, maxExclusive);
        }

        private bool TryGetRandomSpawnPosition(float minDistance, float maxDistance, out Vector3 position)
        {
            float angle = NextRange(0f, 360f) * Mathf.Deg2Rad;
            float distance = NextRange(minDistance, maxDistance);

            Vector3 offset = new Vector3(
                Mathf.Cos(angle) * distance,
                0f,
                Mathf.Sin(angle) * distance
            );

            // ランダムなプレイヤーの周囲にスポーン
            var target = GetRandomPlayerTransform();
            var rawPosition = (target != null ? target.position : Vector3.zero) + offset;

            // NavMesh 上の最寄り点にスナップ（地形の凹凸・NavMesh 外スポーンを防止）
            if (UnityEngine.AI.NavMesh.SamplePosition(rawPosition, out var hit, 10f, UnityEngine.AI.NavMesh.AllAreas))
            {
                position = hit.position;
                return true;
            }

            position = default;
            return false;
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

        /// <summary>
        /// 到達不能エネミーの静かな回収（キルカウント・ドロップ・ウェーブ通知なし）。
        /// クライアントには Death SyncType を送信してプロキシを破棄させる。
        /// </summary>
        private void OnEnemySilentRemoval(SurvivorEnemyController enemy)
        {
            // 次回定期同期で Death を送信（WriteEnemyStates の個別呼び出しは ActiveCount を破壊するため使わない）
            if (_enemyNetworkIds.TryGetValue(enemy, out var networkId))
            {
                _pendingDeaths.Add(new SurvivorNetworkEnemyStateSnapshot
                {
                    NetworkId = networkId,
                    EnemyMasterId = enemy.EnemyId,
                    PositionX = enemy.transform.position.x,
                    PositionY = enemy.transform.position.y,
                    PositionZ = enemy.transform.position.z,
                    CurrentHp = 0,
                    SyncType = EnemySyncType.Death
                });

                _enemyByNetworkId.Remove(networkId);
                _spawnedNetworkIds.Remove(networkId);
            }
            _enemyNetworkIds.Remove(enemy);
            _activeEnemies.Remove(enemy);
            DecrementActiveCount(enemy.EnemyId);
            ReturnToPool(enemy);
        }

        private void OnEnemyDeath(SurvivorEnemyController enemy)
        {
            Debug.Log($"[SurvivorEnemySpawner] EnemyDeath: id={enemy.EnemyId}, boss={enemy.IsBoss}, active={_activeEnemies.Count - 1}, time={Time.time:F1}s");

            // 次回定期同期で Death を送信（WriteEnemyStates の個別呼び出しは ActiveCount を破壊するため使わない）
            if (_enemyNetworkIds.TryGetValue(enemy, out var networkId))
            {
                _pendingDeaths.Add(new SurvivorNetworkEnemyStateSnapshot
                {
                    NetworkId = networkId,
                    EnemyMasterId = enemy.EnemyId,
                    PositionX = enemy.transform.position.x,
                    PositionY = enemy.transform.position.y,
                    PositionZ = enemy.transform.position.z,
                    CurrentHp = 0,
                    SyncType = EnemySyncType.Death
                });
            }

            if (_enemyNetworkIds.TryGetValue(enemy, out var removedNetworkId))
            {
                _enemyByNetworkId.Remove(removedNetworkId);
            }
            _enemyNetworkIds.Remove(enemy);
            _activeEnemies.Remove(enemy);
            DecrementActiveCount(enemy.EnemyId);
            _onEnemyKilled.OnNext(enemy);

            // 死亡アニメーション再生後にプールに戻す（マスターデータから時間を取得）
            var deathDelay = enemy.DeathAnimDuration;
            Observable.Timer(TimeSpan.FromSeconds(deathDelay))
                .Subscribe(_ => ReturnToPool(enemy))
                .AddTo(this);

            // ウェーブサービスに通知（ボスかどうかも伝える）
            if (_runnerService.IsServer)
            {
                _waveManager.OnEnemyKilled(enemy.IsBoss);
            }
        }

        public bool TryGetEnemyByNetworkId(int networkId, out SurvivorEnemyController enemy)
        {
            return _enemyByNetworkId.TryGetValue(networkId, out enemy);
        }

        private void SetAllEnemiesPaused(bool paused)
        {
            foreach (var enemy in _activeEnemies)
            {
                if (enemy != null) enemy.SetPaused(paused);
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
            _activeCountByEnemyId.Clear();
            _nextNetworkId = 0;
            _enemyNetworkIds.Clear();
            _enemyByNetworkId.Clear();
            _spawnedNetworkIds.Clear();
            _pendingDeaths.Clear();
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
