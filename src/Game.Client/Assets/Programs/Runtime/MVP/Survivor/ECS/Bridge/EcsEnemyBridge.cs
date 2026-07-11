using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Client.MasterData;
using Game.Library.Shared.Dto;
using Game.Shared.Events;
using Game.Shared.Extensions;
using Game.Shared.Network.Fusion;
using Game.Shared.Network.Survivor;
using Game.Shared.Services;
using Game.MVP.Survivor.Enemy;
using Game.MVP.Survivor.Services;
using R3;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;
using VContainer;

namespace Game.MVP.Survivor.ECS
{
    /// <summary>
    /// ECS WorldとManaged世界の接続役
    /// WaveManagerからのウェーブ情報をECSに伝達し、
    /// 死亡通知をItemSpawner等に転送する
    /// </summary>
    public class EcsEnemyBridge : MonoBehaviour, IEnemySystemBridge
    {
        // Profiler markers
        private static readonly ProfilerMarker s_bridgeUpdateMarker = new("ProfilerMarker.ECS.BridgeUpdate");
        private static readonly ProfilerMarker s_spawnRequestMarker = new("ProfilerMarker.ECS.SpawnRequest");

        [Header("Pool Settings")]
        [SerializeField] private int _poolSizePerEnemy = 20;

        [Header("Spawn Settings")]
        [SerializeField] private float _defaultMinSpawnDistance = 12f;
        [SerializeField] private float _defaultMaxSpawnDistance = 18f;

        // DI
        [Inject] private IAddressableAssetService _assetService;
        [Inject] private IMasterDataService _masterDataService;
        [Inject] private IFusionRunnerService _runnerService;
        private MemoryDatabase MemoryDatabase => _masterDataService.MemoryDatabase;

        // State
        private World _ecsWorld;
        private readonly List<Transform> _playerTransforms = new();
        private SurvivorStageWaveManager _waveManager;
        private bool _isInitialized;
        private bool _isSpawning;
        private uint _spawnSeed;

        // Wave state
        private WaveSpawnInfo _currentSpawnInfo;
        private List<WaveEnemySpawnInfo> _enemySpawnList;
        private int _currentSpawnIndex;
        private float _spawnTimer;
        private int _remainingSpawnCount;

        // GOプール（敵IDごと）
        private readonly Dictionary<int, Queue<EcsEnemyProxy>> _pools = new();
        private readonly Dictionary<int, GameObject> _enemyPrefabs = new();
        private readonly List<EcsEnemyProxy> _activeProxies = new();

        // Entity → Proxy マッピング
        private readonly Dictionary<Entity, EcsEnemyProxy> _entityProxyMap = new();

        // IDeathNotifier
        private readonly Subject<DeathEventData> _onDeathEvent = new();
        public Observable<DeathEventData> OnDeathEvent => _onDeathEvent;

        // ネットワーク同期
        private const float EnemySyncInterval = 0.1f; // 10Hz
        private float _enemySyncTimer;
        private int _nextNetworkId;
        private readonly Dictionary<Entity, int> _entityNetworkIds = new();

        // L1-4: 事前確保バッファで 10Hz 同期の alloc を排除。
        // SurvivorFusionEnemyBatchSync.MaxEnemies (= 512) と一致させる。
        private const int SyncBufferCapacity = 512;
        private SurvivorNetworkEnemyStateSnapshot[] _syncSnapshotBuffer;
        // CLAUDE.md 制約対応: Spawn/Death の個別 WriteEnemyStates 呼出を定期同期に統合するための pending queue
        private readonly HashSet<int> _spawnedNetworkIds = new();
        private readonly List<SurvivorNetworkEnemyStateSnapshot> _pendingDeaths = new();

        // Systems cache
        private PlayerPositionUpdateSystem _playerPositionSystem;
        private SpawnProcessSystem _spawnProcessSystem;
        private EnemyDeathCleanupSystem _deathCleanupSystem;

        public void SetPlayer(Transform player)
        {
            _playerTransforms.Clear();
            if (player != null)
                _playerTransforms.Add(player);
        }

        public void AddPlayer(Transform player)
        {
            if (player != null && !_playerTransforms.Contains(player))
                _playerTransforms.Add(player);
        }

        public void RemovePlayer(Transform player)
        {
            _playerTransforms.Remove(player);
        }

        private Transform GetRandomPlayerTransform()
        {
            _playerTransforms.RemoveAll(t => t == null);
            if (_playerTransforms.Count == 0) return null;
            return _playerTransforms[UnityEngine.Random.Range(0, _playerTransforms.Count)];
        }

        public async UniTask InitializeAsync(SurvivorStageWaveManager waveManager)
        {
            _waveManager = waveManager;
            _spawnSeed = (uint)UnityEngine.Random.Range(1, int.MaxValue);

            // L1-4: 同期スナップショットバッファを 1 度だけ確保
            if (_syncSnapshotBuffer == null)
            {
                _syncSnapshotBuffer = new SurvivorNetworkEnemyStateSnapshot[SyncBufferCapacity];
            }

            // ECS World生成
            _ecsWorld = EcsWorldBootstrap.CreateWorld();

            // システムキャッシュ
            _playerPositionSystem = EcsWorldBootstrap.GetSystem<PlayerPositionUpdateSystem>();
            _spawnProcessSystem = EcsWorldBootstrap.GetSystem<SpawnProcessSystem>();
            _deathCleanupSystem = EcsWorldBootstrap.GetSystem<EnemyDeathCleanupSystem>();

            // コールバック設定
            _deathCleanupSystem.OnEnemyDied = OnEnemyDied;
            _spawnProcessSystem.OnEntitySpawned = OnEntitySpawned;

            // GameObjectプール生成
            var allEnemies = MemoryDatabase.SurvivorEnemyMasterTable.All;
            foreach (var enemy in allEnemies)
            {
                if (!_enemyPrefabs.ContainsKey(enemy.Id))
                {
                    var prefab = await _assetService.LoadAssetAsync<GameObject>(enemy.AssetName);
                    _enemyPrefabs[enemy.Id] = prefab;

                    // プール初期化
                    _pools[enemy.Id] = new Queue<EcsEnemyProxy>();
                    for (int i = 0; i < _poolSizePerEnemy; i++)
                    {
                        var proxy = CreateProxy(enemy.Id);
                        proxy.gameObject.SetActive(false);
                        _pools[enemy.Id].Enqueue(proxy);
                    }
                }
            }

            // ウェーブ変更を購読
            _waveManager.CurrentWave
                .Where(wave => wave > 0)
                .Subscribe(_ => OnWaveChanged())
                .AddTo(this);

            _isInitialized = true;
            Debug.Log($"[EcsEnemyBridge] Initialized: enemyTypes={_enemyPrefabs.Count}");
        }

        private EcsEnemyProxy CreateProxy(int enemyId)
        {
            if (!_enemyPrefabs.TryGetValue(enemyId, out var prefab))
                return null;

            var instance = Instantiate(prefab, transform);

            // enabled=false ではなく Destroy: GetComponentInParent<ICombatTarget>() が
            // disabled Controller を検出してダメージルーティングが誤動作するのを防止
            if (instance.TryGetComponent<Enemy.SurvivorEnemyController>(out var controller))
            {
                controller.StripForProxy();
            }

            // EcsEnemyProxy はプレハブに事前配置済み（disabled）、有効化するだけ
            instance.TryGetComponent<EcsEnemyProxy>(out var proxy);
            proxy.enabled = true;

            return proxy;
        }

        private void OnWaveChanged()
        {
            _currentSpawnInfo = _waveManager.GetSpawnInfo();
            _enemySpawnList = new List<WaveEnemySpawnInfo>(_waveManager.GetEnemySpawnList());
            _currentSpawnIndex = 0;
            _spawnTimer = 0f;
            _remainingSpawnCount = _currentSpawnInfo.EnemyCount;
            _isSpawning = true;
        }

        private void Update()
        {
            if (!_isInitialized || _ecsWorld == null || !_ecsWorld.IsCreated)
                return;

            using (s_bridgeUpdateMarker.Auto())
            {
                // プレイヤー座標をECSに同期
                if (_playerPositionSystem != null)
                {
                    _playerPositionSystem.PlayerPositions.Clear();
                    foreach (var t in _playerTransforms)
                    {
                        if (t != null)
                        {
                            var pos = t.position;
                            _playerPositionSystem.PlayerPositions.Add(new float3(pos.x, pos.y, pos.z));
                        }
                    }
                }

                // スポーン処理
                if (_isSpawning && _playerTransforms.Count > 0)
                {
                    _spawnTimer -= Time.deltaTime;
                    if (_spawnTimer <= 0f && _remainingSpawnCount > 0)
                    {
                        SpawnNextEnemy();
                    }
                }

                // カスタムWorldの時間を設定（デフォルトWorldと異なり自動更新されない）
                _ecsWorld.SetTime(new Unity.Core.TimeData(
                    elapsedTime: Time.time,
                    deltaTime: Time.deltaTime));

                // ECS Worldを更新
                _ecsWorld.GetExistingSystemManaged<SimulationSystemGroup>()?.Update();
                _ecsWorld.GetExistingSystemManaged<PresentationSystemGroup>()?.Update();

                // ネットワーク同期（サーバー時のみ）
                if (_runnerService.TryGet<SurvivorFusionEnemyBatchSync>(out var batchSync))
                {
                    // 新規エンティティにネットワークIDを割り当て、Spawnスナップショットを送信
                    TrackNewEntitiesForNetwork(batchSync);

                    _enemySyncTimer -= Time.deltaTime;
                    if (_enemySyncTimer <= 0f)
                    {
                        _enemySyncTimer = EnemySyncInterval;
                        SyncEnemyStatesToNetwork(batchSync);
                    }
                }
            }
        }

        /// <summary>
        /// 新規生成されたエンティティにネットワークIDを割り当てる。
        /// CLAUDE.md 制約により個別 WriteEnemyStates 呼出は行わず、Spawn 通知は
        /// 次回の <see cref="SyncEnemyStatesToNetwork"/> で <see cref="_spawnedNetworkIds"/>
        /// 未登録の active entity を Spawn 扱いで送信することで実現する。
        /// </summary>
        private void TrackNewEntitiesForNetwork(SurvivorFusionEnemyBatchSync batchSync)
        {
            var entityManager = _ecsWorld.EntityManager;
            var query = entityManager.CreateEntityQuery(typeof(EnemyData), typeof(LocalTransform), typeof(EnemyAliveTag));
            using var entities = query.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities)
            {
                if (_entityNetworkIds.ContainsKey(entity)) continue;
                var networkId = ++_nextNetworkId;
                _entityNetworkIds[entity] = networkId;
            }
        }

        private void SyncEnemyStatesToNetwork(SurvivorFusionEnemyBatchSync batchSync)
        {
            if (_ecsWorld == null || !_ecsWorld.IsCreated) return;
            if (_syncSnapshotBuffer == null) return; // InitializeAsync 前防御

            var entityManager = _ecsWorld.EntityManager;
            var query = entityManager.CreateEntityQuery(typeof(EnemyData), typeof(LocalTransform), typeof(EnemyAliveTag));
            using var entities = query.ToEntityArray(Allocator.Temp);

            if (entities.Length == 0 && _pendingDeaths.Count == 0) return;

            // L1-4: 事前確保バッファに直接書き込み（alloc 排除）
            int activeFill = Mathf.Min(entities.Length, SyncBufferCapacity);
            for (int i = 0; i < activeFill; i++)
            {
                var entity = entities[i];
                var data = entityManager.GetComponentData<EnemyData>(entity);
                var lt = entityManager.GetComponentData<LocalTransform>(entity);
                _entityNetworkIds.TryGetValue(entity, out var netId);

                // Velocity 計算
                float velocityX = 0f, velocityY = 0f, velocityZ = 0f;
                if (entityManager.HasComponent<EnemyAIState>(entity))
                {
                    var aiState = entityManager.GetComponentData<EnemyAIState>(entity);
                    if (aiState.CurrentState == EcsEnemyAIStateType.Chase)
                    {
                        float3 dir;
                        if (entityManager.HasComponent<EnemySteeringResult>(entity))
                        {
                            var steering = entityManager.GetComponentData<EnemySteeringResult>(entity);
                            if (steering.HasObstacle)
                            {
                                dir = steering.SteeringDirection;
                            }
                            else
                            {
                                var chase = entityManager.GetComponentData<ChaseTarget>(entity);
                                dir = chase.Position - lt.Position;
                                dir = math.lengthsq(dir) > 0.001f ? math.normalize(dir) : float3.zero;
                            }
                        }
                        else
                        {
                            var chase = entityManager.GetComponentData<ChaseTarget>(entity);
                            dir = chase.Position - lt.Position;
                            dir = math.lengthsq(dir) > 0.001f ? math.normalize(dir) : float3.zero;
                        }
                        velocityX = dir.x * data.MoveSpeed;
                        velocityY = dir.y * data.MoveSpeed;
                        velocityZ = dir.z * data.MoveSpeed;
                    }
                }

                // CLAUDE.md 制約対応: 未送信 entity は Spawn 扱いで送信、以降 PositionUpdate
                EnemySyncType syncType;
                if (!_spawnedNetworkIds.Contains(netId))
                {
                    syncType = EnemySyncType.Spawn;
                    _spawnedNetworkIds.Add(netId);
                }
                else
                {
                    syncType = EnemySyncType.PositionUpdate;
                }

                _syncSnapshotBuffer[i] = new SurvivorNetworkEnemyStateSnapshot
                {
                    NetworkId = netId,
                    EnemyMasterId = data.EnemyId,
                    PositionX = lt.Position.x,
                    PositionY = lt.Position.y,
                    PositionZ = lt.Position.z,
                    VelocityX = velocityX,
                    VelocityY = velocityY,
                    VelocityZ = velocityZ,
                    CurrentHp = data.CurrentHp,
                    SyncType = syncType
                };
            }

            // 保留中の Death を末尾に追加（バッファ余剰範囲のみ）
            int deathFill = Mathf.Min(_pendingDeaths.Count, SyncBufferCapacity - activeFill);
            for (int i = 0; i < deathFill; i++)
            {
                _syncSnapshotBuffer[activeFill + i] = _pendingDeaths[i];
            }
            _pendingDeaths.Clear();

            int totalCount = activeFill + deathFill;
            batchSync.WriteEnemyStates(_syncSnapshotBuffer, totalCount);
        }

        private void SpawnNextEnemy()
        {
            using (s_spawnRequestMarker.Auto())
            {
                if (_enemySpawnList == null || _enemySpawnList.Count == 0)
                    return;

                if (_currentSpawnIndex >= _enemySpawnList.Count)
                    _currentSpawnIndex = 0;

                var spawnInfo = _enemySpawnList[_currentSpawnIndex];

                if (!MemoryDatabase.SurvivorEnemyMasterTable.TryFindById(spawnInfo.EnemyId, out var enemyMaster))
                    return;

                // スポーン位置をBurst Jobで計算
                float minDist = spawnInfo.MinSpawnDistance > 0 ? spawnInfo.MinSpawnDistance : _defaultMinSpawnDistance;
                float maxDist = spawnInfo.MaxSpawnDistance > 0 ? spawnInfo.MaxSpawnDistance : _defaultMaxSpawnDistance;

                var positions = new NativeArray<float3>(1, Allocator.TempJob);
                var targetPlayer = GetRandomPlayerTransform();
                if (targetPlayer == null)
                {
                    positions.Dispose();
                    return;
                }
                var playerPos = new float3(targetPlayer.position.x, targetPlayer.position.y, targetPlayer.position.z);

                SpawnPositionCalculator.CalculateImmediate(1, playerPos, minDist, maxDist, _spawnSeed++, positions);

                float3 spawnPosition = positions[0];
                positions.Dispose();

                // ECSにスポーンリクエストを作成
                var entityManager = _ecsWorld.EntityManager;
                var requestEntity = entityManager.CreateEntity(typeof(EnemySpawnRequest));

                entityManager.SetComponentData(requestEntity, new EnemySpawnRequest
                {
                    EnemyId = enemyMaster.Id,
                    Position = spawnPosition,
                    MaxHp = Mathf.RoundToInt(enemyMaster.BaseHp * _currentSpawnInfo.EnemyHealthMultiplier),
                    AttackDamage = Mathf.RoundToInt(enemyMaster.BaseDamage * _currentSpawnInfo.EnemyDamageMultiplier),
                    MoveSpeed = enemyMaster.MoveSpeed.ToUnit() * _currentSpawnInfo.EnemySpeedMultiplier,
                    AttackRange = enemyMaster.AttackRange.ToUnit(),
                    AttackCooldown = enemyMaster.AttackCooldown.ToSeconds(),
                    HitStunDuration = enemyMaster.HitStunDuration.ToSeconds(),
                    RotationSpeed = enemyMaster.RotationSpeed,
                    DeathAnimDuration = enemyMaster.DeathAnimDuration.ToSeconds(),
                    AttackRangeExitMultiplier = enemyMaster.AttackRangeExitMultiplier / 100f,
                    ExperienceValue = Mathf.RoundToInt(enemyMaster.ExperienceValue * _currentSpawnInfo.ExperienceMultiplier),
                    EnemyType = enemyMaster.EnemyType,
                    ItemDropGroupId = spawnInfo.ItemDropGroupId,
                    ExpDropGroupId = spawnInfo.ExpDropGroupId
                });

                _remainingSpawnCount--;
                _spawnTimer = spawnInfo.SpawnInterval;
                _currentSpawnIndex++;

                if (_remainingSpawnCount <= 0)
                {
                    _isSpawning = false;
                }
            }
        }

        /// <summary>
        /// SpawnProcessSystemからのコールバック
        /// ECSエンティティ生成後にGameObjectをプールから取得して紐付ける
        /// </summary>
        private void OnEntitySpawned(Entity entity, EnemySpawnRequest request)
        {
            var proxy = GetFromPool(request.EnemyId);
            if (proxy == null)
            {
                proxy = CreateProxy(request.EnemyId);
            }

            if (proxy == null)
            {
                Debug.LogError($"[EcsEnemyBridge] Failed to create proxy for enemy {request.EnemyId}");
                return;
            }

            // GameObjectの位置を設定して有効化
            proxy.transform.position = new Vector3(request.Position.x, request.Position.y, request.Position.z);
            proxy.gameObject.SetActive(true);

            // ECSエンティティとProxyを紐付け（_runnerService は PhysicsScene・DeltaTime 取得に使用）
            proxy.Initialize(entity, _ecsWorld, request.EnemyId, _runnerService);

            // ManagedGameObjectReferenceを設定（SetComponentDataはマネージドclass型にも対応）
            _ecsWorld.EntityManager.SetComponentData(entity, new ManagedGameObjectReference
            {
                GameObject = proxy.gameObject,
                GameObjectInstanceId = proxy.gameObject.GetInstanceID()
            });

            _activeProxies.Add(proxy);
            _entityProxyMap[entity] = proxy;
        }

        /// <summary>
        /// EnemyDeathCleanupSystemからのコールバック
        /// </summary>
        private void OnEnemyDied(EnemyDeathInfo deathInfo)
        {
            // IDeathNotifier経由でItemSpawner等に通知
            _onDeathEvent.OnNext(new DeathEventData(
                new Vector3(deathInfo.Position.x, deathInfo.Position.y, deathInfo.Position.z),
                deathInfo.ItemDropGroupId,
                deathInfo.ExpDropGroupId
            ));

            // WaveManagerに通知
            bool isBoss = deathInfo.EnemyType == 3;
            _waveManager?.OnEnemyKilled(isBoss);

            // CLAUDE.md 制約対応: Death 通知は定期同期で統合する pending queue に追加
            if (_entityNetworkIds.TryGetValue(deathInfo.Entity, out var deadNetId))
            {
                _pendingDeaths.Add(new SurvivorNetworkEnemyStateSnapshot
                {
                    NetworkId = deadNetId,
                    EnemyMasterId = deathInfo.EnemyType,
                    PositionX = deathInfo.Position.x,
                    PositionY = deathInfo.Position.y,
                    PositionZ = deathInfo.Position.z,
                    VelocityX = 0f,
                    VelocityY = 0f,
                    VelocityZ = 0f,
                    SyncType = EnemySyncType.Death
                });
                _spawnedNetworkIds.Remove(deadNetId);
                _entityNetworkIds.Remove(deathInfo.Entity);
            }

            // Proxyを処理
            if (_entityProxyMap.TryGetValue(deathInfo.Entity, out var proxy))
            {
                _entityProxyMap.Remove(deathInfo.Entity);
                _activeProxies.Remove(proxy);

                proxy.SetDead();

                // 死亡アニメーション後にプールに戻す
                float delay = deathInfo.DeathAnimDuration;
                Observable.Timer(TimeSpan.FromSeconds(delay))
                    .Subscribe(_ => ReturnToPool(proxy))
                    .AddTo(this);
            }
        }

        private EcsEnemyProxy GetFromPool(int enemyId)
        {
            if (!_pools.TryGetValue(enemyId, out var pool))
                return null;

            while (pool.Count > 0)
            {
                var proxy = pool.Dequeue();
                if (proxy != null)
                    return proxy;
            }

            return null;
        }

        private void ReturnToPool(EcsEnemyProxy proxy)
        {
            if (proxy == null) return;

            var enemyId = proxy.EnemyId;
            proxy.ResetForPool();

            if (_pools.TryGetValue(enemyId, out var pool))
            {
                pool.Enqueue(proxy);
            }
        }

        /// <summary>
        /// 全ての敵をクリア
        /// </summary>
        public void ClearAllEnemies()
        {
            foreach (var proxy in _activeProxies.ToArray())
            {
                ReturnToPool(proxy);
            }
            _activeProxies.Clear();
            _entityProxyMap.Clear();
            _entityNetworkIds.Clear();
            _isSpawning = false;

            // ECSエンティティもクリア
            if (_ecsWorld != null && _ecsWorld.IsCreated)
            {
                var entityManager = _ecsWorld.EntityManager;
                var query = entityManager.CreateEntityQuery(typeof(EnemyData));
                entityManager.DestroyEntity(query);
            }
        }

        private void OnDestroy()
        {
            _onDeathEvent.Dispose();

            // ECS Worldを破棄
            EcsWorldBootstrap.DestroyWorld();

            // プール内のProxyを破棄
            foreach (var pool in _pools.Values)
            {
                while (pool.Count > 0)
                {
                    var proxy = pool.Dequeue();
                    if (proxy != null)
                        Destroy(proxy.gameObject);
                }
            }
            _pools.Clear();

            // アクティブなProxyを破棄
            foreach (var proxy in _activeProxies)
            {
                if (proxy != null)
                    Destroy(proxy.gameObject);
            }
            _activeProxies.Clear();

            // プレハブリリース
            foreach (var prefab in _enemyPrefabs.Values)
            {
                _assetService?.Release(prefab);
            }
            _enemyPrefabs.Clear();
        }
    }
}
