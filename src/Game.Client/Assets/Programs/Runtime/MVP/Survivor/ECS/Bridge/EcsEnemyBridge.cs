using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Client.MasterData;
using Game.Shared.Events;
using Game.Shared.Extensions;
using Game.Shared.Services;
using Game.MVP.Survivor.Enemy;
using Game.MVP.Survivor.Services;
using R3;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
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
        private MemoryDatabase MemoryDatabase => _masterDataService.MemoryDatabase;

        // State
        private World _ecsWorld;
        private Transform _playerTransform;
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

        // Systems cache
        private PlayerPositionUpdateSystem _playerPositionSystem;
        private SpawnProcessSystem _spawnProcessSystem;
        private EnemyDeathCleanupSystem _deathCleanupSystem;

        public void SetPlayer(Transform player)
        {
            _playerTransform = player;
        }

        public async UniTask InitializeAsync(SurvivorStageWaveManager waveManager)
        {
            _waveManager = waveManager;
            _spawnSeed = (uint)UnityEngine.Random.Range(1, int.MaxValue);

            // ECS World生成
            _ecsWorld = EcsWorldBootstrap.CreateWorld();

            // システムキャッシュ
            _playerPositionSystem = EcsWorldBootstrap.GetSystem<PlayerPositionUpdateSystem>();
            _spawnProcessSystem = EcsWorldBootstrap.GetSystem<SpawnProcessSystem>();
            _deathCleanupSystem = EcsWorldBootstrap.GetSystem<EnemyDeathCleanupSystem>();

            // コールバック設定
            _spawnProcessSystem.OnEntitySpawned = OnEntitySpawned;
            _deathCleanupSystem.OnEnemyDied = OnEnemyDied;

            // 全敵タイプのプレハブを事前読み込み
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
            Debug.Log($"[EcsEnemyBridge] Initialized with {_enemyPrefabs.Count} enemy types (ECS mode)");
        }

        private EcsEnemyProxy CreateProxy(int enemyId)
        {
            if (!_enemyPrefabs.TryGetValue(enemyId, out var prefab))
                return null;

            var instance = Instantiate(prefab, transform);

            // 既存のSurvivorEnemyControllerを無効化（ECSが制御するため）
            var existingController = instance.GetComponent<Enemy.SurvivorEnemyController>();
            if (existingController != null)
            {
                existingController.enabled = false;
            }

            // NavMeshAgentも無効化（ECSが直進移動を担当）
            var navAgent = instance.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (navAgent != null)
            {
                navAgent.enabled = false;
            }

            // EcsEnemyProxyを追加
            if (!instance.TryGetComponent<EcsEnemyProxy>(out var proxy))
            {
                proxy = instance.AddComponent<EcsEnemyProxy>();
            }

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
                if (_playerTransform != null && _playerPositionSystem != null)
                {
                    var pos = _playerTransform.position;
                    _playerPositionSystem.PlayerPosition = new float3(pos.x, pos.y, pos.z);
                    _playerPositionSystem.IsActive = true;
                }

                // スポーン処理
                if (_isSpawning && _playerTransform != null)
                {
                    _spawnTimer -= Time.deltaTime;
                    if (_spawnTimer <= 0f && _remainingSpawnCount > 0)
                    {
                        SpawnNextEnemy();
                    }
                }

                // ECS Worldを更新
                _ecsWorld.GetExistingSystemManaged<SimulationSystemGroup>()?.Update();
                _ecsWorld.GetExistingSystemManaged<PresentationSystemGroup>()?.Update();
            }
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
                var playerPos = new float3(_playerTransform.position.x, _playerTransform.position.y, _playerTransform.position.z);

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

            // ECSエンティティとProxyを紐付け
            proxy.Initialize(entity, _ecsWorld);

            // ManagedGameObjectReferenceを設定
            var entityManager = _ecsWorld.EntityManager;
            if (entityManager.HasComponent<ManagedGameObjectReference>(entity))
            {
                entityManager.SetComponentData(entity, new ManagedGameObjectReference
                {
                    GameObject = proxy.gameObject,
                    GameObjectInstanceId = proxy.gameObject.GetInstanceID()
                });
            }

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

            // GameObjectのEnemyIdを取得するため、EnemyDataから情報が必要
            // Proxyにはnameからパースするか、別途IDを保持する
            proxy.ResetForPool();

            // 全プールから探して戻す（簡略化: transformの親がこのオブジェクト）
            foreach (var kvp in _pools)
            {
                if (_enemyPrefabs.ContainsKey(kvp.Key))
                {
                    kvp.Value.Enqueue(proxy);
                    return;
                }
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
                _assetService?.ReleaseAsset(prefab);
            }
            _enemyPrefabs.Clear();
        }
    }
}
