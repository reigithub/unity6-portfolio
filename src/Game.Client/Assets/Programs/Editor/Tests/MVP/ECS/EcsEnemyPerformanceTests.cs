using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Game.MVP.Survivor.ECS;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Game.Tests.MVP.ECS
{
    /// <summary>
    /// ECS敵システムの性能比較テスト
    /// MonoBehaviour方式との定量比較データを取得し、ECS + Jobs + Burstの優位性を実証
    /// </summary>
    [TestFixture]
    public class EcsEnemyPerformanceTests
    {
        // テストパラメータ
        private static readonly int[] EnemyCounts = { 100, 500, 1000, 2000, 5000 };
        private const int DefaultIterations = 1000;
        private const int WarmupIterations = 100;

        // ログ出力
        private StringBuilder _logBuilder;
        private string _logFilePath;

        private World _testWorld;
        private EntityManager _entityManager;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // ログファイルのパスを設定
            var logDir = Path.Combine(Application.dataPath, "..", "Logs", "PerformanceTests");
            Directory.CreateDirectory(logDir);
            _logFilePath = Path.Combine(logDir, $"EcsEnemyPerformance_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        }

        [SetUp]
        public void SetUp()
        {
            _logBuilder = new StringBuilder();
            _testWorld = new World("PerfTestWorld");
            _entityManager = _testWorld.EntityManager;

            // システム登録
            var simGroup = _testWorld.GetOrCreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(_testWorld.GetOrCreateSystem<EnemyAIStateSystem>());
            simGroup.AddSystemToUpdateList(_testWorld.GetOrCreateSystem<EnemyMovementSystem>());
            simGroup.AddSystemToUpdateList(_testWorld.GetOrCreateSystem<EnemyDamageSystem>());
            simGroup.AddSystemToUpdateList(_testWorld.GetOrCreateSystemManaged<PlayerPositionUpdateSystem>());
            simGroup.AddSystemToUpdateList(_testWorld.GetOrCreateSystemManaged<EnemyDeathCleanupSystem>());
            simGroup.SortSystems();
        }

        [TearDown]
        public void TearDown()
        {
            if (_testWorld != null && _testWorld.IsCreated)
            {
                _testWorld.Dispose();
            }

            // ログ出力
            if (_logBuilder != null && _logBuilder.Length > 0)
            {
                var logContent = _logBuilder.ToString();
                Debug.Log(logContent);
                File.AppendAllText(_logFilePath, logContent + "\n");
            }
        }

        #region Helper Methods

        private void CreateEnemyEntities(int count, float3 playerPosition)
        {
            var random = new Unity.Mathematics.Random(42);

            for (int i = 0; i < count; i++)
            {
                float angle = random.NextFloat(0f, math.PI * 2f);
                float distance = random.NextFloat(5f, 30f);
                float3 position = playerPosition + new float3(math.cos(angle) * distance, 0, math.sin(angle) * distance);

                var entity = _entityManager.CreateEntity(
                    typeof(EnemyData),
                    typeof(EnemyAIState),
                    typeof(ChaseTarget),
                    typeof(DamageEvent),
                    typeof(EnemyAliveTag),
                    typeof(EnemyDeadTag),
                    typeof(LocalTransform)
                );

                _entityManager.SetComponentData(entity, new EnemyData
                {
                    EnemyId = 1,
                    EnemyType = 1,
                    CurrentHp = 100,
                    MaxHp = 100,
                    AttackDamage = 10,
                    MoveSpeed = 5f,
                    AttackRange = 2f,
                    AttackCooldown = 1f,
                    HitStunDuration = 0.5f,
                    RotationSpeed = 10f,
                    DeathAnimDuration = 1f,
                    AttackRangeExitMultiplier = 1.5f,
                    ExperienceValue = 10,
                    ItemDropGroupId = 1,
                    ExpDropGroupId = 1
                });

                _entityManager.SetComponentData(entity, new EnemyAIState
                {
                    CurrentState = EcsEnemyAIStateType.Chase,
                    StateTimer = 0f
                });

                _entityManager.SetComponentData(entity, new ChaseTarget { Position = playerPosition });
                _entityManager.SetComponentData(entity, new DamageEvent());
                _entityManager.SetComponentData(entity, LocalTransform.FromPosition(position));
                _entityManager.SetComponentEnabled<EnemyDeadTag>(entity, false);
            }
        }

        private void LogHeader(string testName, int enemyCount, int iterations)
        {
            _logBuilder.AppendLine($"=== {testName} ===");
            _logBuilder.AppendLine($"Enemy Count: {enemyCount} | Iterations: {iterations}");
        }

        private void LogResult(string label, double totalMs, int iterations, int entityCount, long memoryBytes = 0)
        {
            double perFrame = totalMs / iterations;
            double perEntity = entityCount > 0 ? (totalMs * 1000.0 / iterations / entityCount) : 0; // microseconds

            _logBuilder.Append($"  {label,-25}: {totalMs,10:F2}ms ({perFrame,8:F4}ms/frame, {perEntity,8:F3}us/entity)");
            if (memoryBytes > 0)
                _logBuilder.Append($" | Memory: {memoryBytes:N0} bytes");
            _logBuilder.AppendLine();
        }

        private void LogSpeedup(double monoMs, double ecsMs)
        {
            if (ecsMs > 0)
            {
                double speedup = monoMs / ecsMs;
                _logBuilder.AppendLine($"  Speedup: {speedup:F2}x");
            }
            _logBuilder.AppendLine();
        }

        #endregion

        #region Spawn Position Calculation

        [Test]
        public void SpawnPositionCalc_SequentialVsBurstParallel(
            [ValueSource(nameof(EnemyCounts))] int enemyCount)
        {
            var playerPos = new float3(50f, 0f, 50f);
            float minDist = 12f;
            float maxDist = 18f;
            int iterations = DefaultIterations;

            LogHeader("Spawn Position Calculation: Sequential vs Burst Parallel", enemyCount, iterations);

            // Warmup
            var warmupResults = new NativeArray<float3>(enemyCount, Allocator.TempJob);
            for (int i = 0; i < WarmupIterations; i++)
            {
                SpawnPositionCalculator.CalculateImmediate(enemyCount, playerPos, minDist, maxDist, (uint)i + 1, warmupResults);
            }
            warmupResults.Dispose();

            // --- Sequential (simulating MonoBehaviour approach) ---
            long memBefore = GC.GetTotalMemory(true);
            var sw = Stopwatch.StartNew();

            var seqRandom = new System.Random(42);
            var seqResults = new Vector3[enemyCount];
            for (int iter = 0; iter < iterations; iter++)
            {
                for (int i = 0; i < enemyCount; i++)
                {
                    float angle = (float)(seqRandom.NextDouble() * Math.PI * 2.0);
                    float distance = (float)(seqRandom.NextDouble() * (maxDist - minDist) + minDist);
                    seqResults[i] = new Vector3(
                        playerPos.x + Mathf.Cos(angle) * distance,
                        0f,
                        playerPos.z + Mathf.Sin(angle) * distance
                    );
                }
            }

            sw.Stop();
            long seqMemory = GC.GetTotalMemory(false) - memBefore;
            double seqMs = sw.Elapsed.TotalMilliseconds;

            // --- Burst Parallel Job ---
            var burstResults = new NativeArray<float3>(enemyCount, Allocator.TempJob);
            memBefore = GC.GetTotalMemory(true);
            sw.Restart();

            for (int iter = 0; iter < iterations; iter++)
            {
                SpawnPositionCalculator.CalculateImmediate(
                    enemyCount, playerPos, minDist, maxDist, (uint)iter + 1, burstResults);
            }

            sw.Stop();
            long burstMemory = GC.GetTotalMemory(false) - memBefore;
            double burstMs = sw.Elapsed.TotalMilliseconds;
            burstResults.Dispose();

            LogResult("MonoBehaviour (Sequential)", seqMs, iterations, enemyCount, Math.Max(0, seqMemory));
            LogResult("ECS+Burst (Parallel Job)", burstMs, iterations, enemyCount, Math.Max(0, burstMemory));
            LogSpeedup(seqMs, burstMs);

            // Burstが有効な場合のみ性能アサーション（CI等でBurst無効時はJobオーバーヘッドで逆転する）
            if (BurstCompiler.IsEnabled)
            {
                Assert.Less(burstMs, seqMs * 2.0,
                    $"ECS+Burstが逐次処理の2倍以上遅い: Sequential={seqMs:F2}ms, Burst={burstMs:F2}ms");
            }
            else
            {
                Debug.LogWarning($"[EcsEnemyPerformanceTests] Burstが無効のためアサーションをスキップ: Sequential={seqMs:F2}ms, Burst={burstMs:F2}ms");
            }
        }

        #endregion

        #region Movement Update

        [Test]
        public void MovementUpdate_IndividualVsBatchJob(
            [ValueSource(nameof(EnemyCounts))] int enemyCount)
        {
            var playerPos = new float3(0f, 0f, 0f);
            int iterations = DefaultIterations;

            LogHeader("Movement Update: Individual Update vs IJobEntity", enemyCount, iterations);

            // --- Sequential (simulating MonoBehaviour per-enemy Update) ---
            var positions = new Vector3[enemyCount];
            var velocities = new Vector3[enemyCount];
            var random = new System.Random(42);

            for (int i = 0; i < enemyCount; i++)
            {
                float angle = (float)(random.NextDouble() * Math.PI * 2.0);
                float dist = (float)(random.NextDouble() * 25.0 + 5.0);
                positions[i] = new Vector3(
                    Mathf.Cos(angle) * dist,
                    0f,
                    Mathf.Sin(angle) * dist
                );
            }

            // Warmup
            float dt = 0.016f;
            float moveSpeed = 5f;
            for (int w = 0; w < WarmupIterations; w++)
            {
                for (int i = 0; i < enemyCount; i++)
                {
                    var dir = (Vector3.zero - positions[i]).normalized;
                    positions[i] += dir * moveSpeed * dt;
                }
            }

            // Reset positions
            for (int i = 0; i < enemyCount; i++)
            {
                float angle = (float)(random.NextDouble() * Math.PI * 2.0);
                float dist = (float)(random.NextDouble() * 25.0 + 5.0);
                positions[i] = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
            }

            long memBefore = GC.GetTotalMemory(true);
            var sw = Stopwatch.StartNew();

            for (int iter = 0; iter < iterations; iter++)
            {
                for (int i = 0; i < enemyCount; i++)
                {
                    var direction = (Vector3.zero - positions[i]).normalized;
                    positions[i] += direction * moveSpeed * dt;
                }
            }

            sw.Stop();
            long seqMemory = GC.GetTotalMemory(false) - memBefore;
            double seqMs = sw.Elapsed.TotalMilliseconds;

            // --- ECS IJobEntity ---
            CreateEnemyEntities(enemyCount, playerPos);

            // Warmup
            var simGroup = _testWorld.GetExistingSystemManaged<SimulationSystemGroup>();
            for (int w = 0; w < WarmupIterations; w++)
            {
                simGroup.Update();
            }

            memBefore = GC.GetTotalMemory(true);
            sw.Restart();

            for (int iter = 0; iter < iterations; iter++)
            {
                simGroup.Update();
            }

            sw.Stop();
            long ecsMemory = GC.GetTotalMemory(false) - memBefore;
            double ecsMs = sw.Elapsed.TotalMilliseconds;

            LogResult("MonoBehaviour (per-Update)", seqMs, iterations, enemyCount, Math.Max(0, seqMemory));
            LogResult("ECS+Burst (IJobEntity)", ecsMs, iterations, enemyCount, Math.Max(0, ecsMemory));
            LogSpeedup(seqMs, ecsMs);
        }

        #endregion

        #region Damage Processing

        [Test]
        public void DamageProcessing_IndividualVsBatchJob(
            [ValueSource(nameof(EnemyCounts))] int enemyCount)
        {
            var playerPos = new float3(0f, 0f, 0f);
            int iterations = DefaultIterations;

            LogHeader("Damage Processing: Individual vs Batch Job", enemyCount, iterations);

            // --- Sequential (simulating MonoBehaviour per-enemy TakeDamage) ---
            var hpArray = new int[enemyCount];
            var isDeadArray = new bool[enemyCount];
            int maxHp = 100;
            int damagePerHit = 10;

            // Warmup
            for (int w = 0; w < WarmupIterations; w++)
            {
                for (int i = 0; i < enemyCount; i++)
                {
                    hpArray[i] = maxHp;
                    isDeadArray[i] = false;
                }
                for (int i = 0; i < enemyCount; i++)
                {
                    hpArray[i] -= damagePerHit;
                    if (hpArray[i] <= 0) isDeadArray[i] = true;
                }
            }

            long memBefore = GC.GetTotalMemory(true);
            var sw = Stopwatch.StartNew();

            for (int iter = 0; iter < iterations; iter++)
            {
                // リセット
                for (int i = 0; i < enemyCount; i++)
                {
                    hpArray[i] = maxHp;
                    isDeadArray[i] = false;
                }

                // ダメージ処理
                for (int i = 0; i < enemyCount; i++)
                {
                    if (isDeadArray[i]) continue;
                    hpArray[i] -= damagePerHit;
                    if (hpArray[i] <= 0)
                    {
                        hpArray[i] = 0;
                        isDeadArray[i] = true;
                    }
                }
            }

            sw.Stop();
            long seqMemory = GC.GetTotalMemory(false) - memBefore;
            double seqMs = sw.Elapsed.TotalMilliseconds;

            // --- ECS Batch Job ---
            CreateEnemyEntities(enemyCount, playerPos);

            // 全エンティティにダメージを設定
            var query = _entityManager.CreateEntityQuery(typeof(DamageEvent), typeof(EnemyAliveTag));

            // Warmup
            for (int w = 0; w < WarmupIterations; w++)
            {
                // ダメージ設定
                var entities = query.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < entities.Length; i++)
                {
                    _entityManager.SetComponentData(entities[i], new DamageEvent { Damage = damagePerHit });
                }
                entities.Dispose();

                _testWorld.GetExistingSystemManaged<SimulationSystemGroup>().Update();

                // HPリセット（ウォームアップ）
                var allEntities = _entityManager.CreateEntityQuery(typeof(EnemyData)).ToEntityArray(Allocator.Temp);
                for (int i = 0; i < allEntities.Length; i++)
                {
                    var data = _entityManager.GetComponentData<EnemyData>(allEntities[i]);
                    data.CurrentHp = maxHp;
                    _entityManager.SetComponentData(allEntities[i], data);

                    _entityManager.SetComponentData(allEntities[i], new EnemyAIState
                    {
                        CurrentState = EcsEnemyAIStateType.Chase,
                        StateTimer = 0f
                    });
                    _entityManager.SetComponentEnabled<EnemyAliveTag>(allEntities[i], true);
                    _entityManager.SetComponentEnabled<EnemyDeadTag>(allEntities[i], false);
                }
                allEntities.Dispose();
            }

            memBefore = GC.GetTotalMemory(true);
            sw.Restart();

            for (int iter = 0; iter < iterations; iter++)
            {
                // ダメージ設定
                var entities = query.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < entities.Length; i++)
                {
                    _entityManager.SetComponentData(entities[i], new DamageEvent { Damage = damagePerHit });
                }
                entities.Dispose();

                _testWorld.GetExistingSystemManaged<SimulationSystemGroup>().Update();

                // HPリセット
                var allEntities = _entityManager.CreateEntityQuery(typeof(EnemyData)).ToEntityArray(Allocator.Temp);
                for (int i = 0; i < allEntities.Length; i++)
                {
                    var data = _entityManager.GetComponentData<EnemyData>(allEntities[i]);
                    data.CurrentHp = maxHp;
                    _entityManager.SetComponentData(allEntities[i], data);

                    _entityManager.SetComponentData(allEntities[i], new EnemyAIState
                    {
                        CurrentState = EcsEnemyAIStateType.Chase,
                        StateTimer = 0f
                    });
                    _entityManager.SetComponentEnabled<EnemyAliveTag>(allEntities[i], true);
                    _entityManager.SetComponentEnabled<EnemyDeadTag>(allEntities[i], false);
                }
                allEntities.Dispose();
            }

            sw.Stop();
            long ecsMemory = GC.GetTotalMemory(false) - memBefore;
            double ecsMs = sw.Elapsed.TotalMilliseconds;

            LogResult("MonoBehaviour (Individual)", seqMs, iterations, enemyCount, Math.Max(0, seqMemory));
            LogResult("ECS+Burst (Batch Job)", ecsMs, iterations, enemyCount, Math.Max(0, ecsMemory));
            LogSpeedup(seqMs, ecsMs);
        }

        #endregion

        #region Full Frame Simulation

        [Test]
        public void FullFrameSimulation_CompareOverallPerformance(
            [ValueSource(nameof(EnemyCounts))] int enemyCount)
        {
            var playerPos = new float3(0f, 0f, 0f);
            int iterations = DefaultIterations;

            LogHeader("Full Frame Simulation (Spawn+Movement+AI+Damage)", enemyCount, iterations);

            // --- Sequential (simulating full MonoBehaviour frame) ---
            var monoPositions = new Vector3[enemyCount];
            var monoHp = new int[enemyCount];
            var monoStates = new int[enemyCount]; // 0=Chase, 1=Attack
            var monoIsDead = new bool[enemyCount];
            float dt = 0.016f;
            float moveSpeed = 5f;
            float attackRange = 2f;
            int maxHp = 100;

            // 初期化
            var rng = new System.Random(42);
            for (int i = 0; i < enemyCount; i++)
            {
                float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
                float dist = (float)(rng.NextDouble() * 25.0 + 5.0);
                monoPositions[i] = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                monoHp[i] = maxHp;
                monoStates[i] = 0;
                monoIsDead[i] = false;
            }

            // Warmup
            for (int w = 0; w < WarmupIterations; w++)
            {
                for (int i = 0; i < enemyCount; i++)
                {
                    if (monoIsDead[i]) continue;

                    var toTarget = Vector3.zero - monoPositions[i];
                    float dist = toTarget.magnitude;

                    if (dist <= attackRange)
                        monoStates[i] = 1;
                    else
                        monoStates[i] = 0;

                    if (monoStates[i] == 0)
                        monoPositions[i] += toTarget.normalized * moveSpeed * dt;
                }
            }

            // Reset
            for (int i = 0; i < enemyCount; i++)
            {
                float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
                float dist = (float)(rng.NextDouble() * 25.0 + 5.0);
                monoPositions[i] = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                monoHp[i] = maxHp;
                monoStates[i] = 0;
                monoIsDead[i] = false;
            }

            long memBefore = GC.GetTotalMemory(true);
            var sw = Stopwatch.StartNew();

            for (int iter = 0; iter < iterations; iter++)
            {
                for (int i = 0; i < enemyCount; i++)
                {
                    if (monoIsDead[i]) continue;

                    // AI state
                    var toTarget = Vector3.zero - monoPositions[i];
                    float dist = toTarget.magnitude;

                    if (dist <= attackRange)
                        monoStates[i] = 1;
                    else
                        monoStates[i] = 0;

                    // Movement
                    if (monoStates[i] == 0 && dist > 0.01f)
                        monoPositions[i] += toTarget.normalized * moveSpeed * dt;

                    // Damage (10% chance per frame for testing)
                    if (iter % 10 == 0)
                    {
                        monoHp[i] -= 5;
                        if (monoHp[i] <= 0)
                        {
                            monoHp[i] = 0;
                            monoIsDead[i] = true;
                        }
                    }
                }

                // Respawn dead for next iteration
                if (iter % 10 == 0)
                {
                    for (int i = 0; i < enemyCount; i++)
                    {
                        if (monoIsDead[i])
                        {
                            monoHp[i] = maxHp;
                            monoIsDead[i] = false;
                        }
                    }
                }
            }

            sw.Stop();
            long seqMemory = GC.GetTotalMemory(false) - memBefore;
            double seqMs = sw.Elapsed.TotalMilliseconds;

            // --- ECS Full Frame ---
            CreateEnemyEntities(enemyCount, playerPos);
            var simGroup = _testWorld.GetExistingSystemManaged<SimulationSystemGroup>();

            // Warmup
            for (int w = 0; w < WarmupIterations; w++)
            {
                simGroup.Update();
            }

            memBefore = GC.GetTotalMemory(true);
            sw.Restart();

            for (int iter = 0; iter < iterations; iter++)
            {
                simGroup.Update();
            }

            sw.Stop();
            long ecsMemory = GC.GetTotalMemory(false) - memBefore;
            double ecsMs = sw.Elapsed.TotalMilliseconds;

            LogResult("MonoBehaviour (Full Frame)", seqMs, iterations, enemyCount, Math.Max(0, seqMemory));
            LogResult("ECS+Burst (Full Frame)", ecsMs, iterations, enemyCount, Math.Max(0, ecsMemory));
            LogSpeedup(seqMs, ecsMs);

            // サマリー出力
            _logBuilder.AppendLine($"  --- Summary ---");
            _logBuilder.AppendLine($"  Enemy Count: {enemyCount} | Iterations: {iterations}");
            _logBuilder.AppendLine($"  MonoBehaviour: {seqMs:F1}ms ({seqMs / iterations:F3}ms/frame, {seqMs * 1000 / iterations / enemyCount:F3}us/entity)");
            _logBuilder.AppendLine($"  ECS+Burst:     {ecsMs:F1}ms ({ecsMs / iterations:F3}ms/frame, {ecsMs * 1000 / iterations / enemyCount:F3}us/entity)");
            if (ecsMs > 0)
                _logBuilder.AppendLine($"  Speedup: {seqMs / ecsMs:F2}x");
            _logBuilder.AppendLine();
        }

        #endregion
    }
}
