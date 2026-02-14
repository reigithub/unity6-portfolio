using System.Collections.Generic;
using Game.MVP.Survivor.ECS;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Game.Tests.MVP.ECS
{
    /// <summary>
    /// ECS敵システムの機能正確性テスト
    /// 各ECSコンポーネントとシステムの動作を検証
    /// </summary>
    [TestFixture]
    public class EcsEnemySystemTests
    {
        private World _testWorld;
        private EntityManager _entityManager;

        /// <summary>テスト用の固定DeltaTime（16ms = 60fps相当）</summary>
        private const float TestDeltaTime = 0.016f;

        [SetUp]
        public void SetUp()
        {
            _testWorld = new World("TestWorld");
            _entityManager = _testWorld.EntityManager;

            // テストに必要なシステムのみ登録（DeathCleanup/PlayerPositionは除外）
            var simGroup = _testWorld.GetOrCreateSystemManaged<SimulationSystemGroup>();

            var aiSystem = _testWorld.GetOrCreateSystem<EnemyAIStateSystem>();
            var moveSystem = _testWorld.GetOrCreateSystem<EnemyMovementSystem>();
            var damageSystem = _testWorld.GetOrCreateSystem<EnemyDamageSystem>();

            simGroup.AddSystemToUpdateList(aiSystem);
            simGroup.AddSystemToUpdateList(moveSystem);
            simGroup.AddSystemToUpdateList(damageSystem);

            simGroup.SortSystems();
        }

        [TearDown]
        public void TearDown()
        {
            if (_testWorld != null && _testWorld.IsCreated)
            {
                _testWorld.Dispose();
            }
        }

        #region Helper Methods

        private Entity CreateEnemyEntity(
            float3 position = default,
            int maxHp = 100,
            float moveSpeed = 5f,
            float attackRange = 2f,
            float attackCooldown = 1f,
            float hitStunDuration = 0.5f,
            float attackRangeExitMultiplier = 1.5f,
            EcsEnemyAIStateType initialState = EcsEnemyAIStateType.Chase)
        {
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
                CurrentHp = maxHp,
                MaxHp = maxHp,
                AttackDamage = 10,
                MoveSpeed = moveSpeed,
                AttackRange = attackRange,
                AttackCooldown = attackCooldown,
                HitStunDuration = hitStunDuration,
                RotationSpeed = 10f,
                DeathAnimDuration = 1f,
                AttackRangeExitMultiplier = attackRangeExitMultiplier,
                ExperienceValue = 10,
                ItemDropGroupId = 1,
                ExpDropGroupId = 1
            });

            _entityManager.SetComponentData(entity, new EnemyAIState
            {
                CurrentState = initialState,
                StateTimer = 0f
            });

            _entityManager.SetComponentData(entity, new ChaseTarget
            {
                Position = float3.zero
            });

            _entityManager.SetComponentData(entity, new DamageEvent
            {
                Damage = 0,
                Knockback = float3.zero
            });

            _entityManager.SetComponentData(entity, LocalTransform.FromPosition(position));

            // EnemyDeadTagは初期無効
            _entityManager.SetComponentEnabled<EnemyDeadTag>(entity, false);

            return entity;
        }

        private void UpdateWorld(float dt = TestDeltaTime)
        {
            // テスト用にWorld時間を設定（DeltaTime > 0が必要）
            _testWorld.PushTime(new TimeData(dt, dt));
            _testWorld.GetExistingSystemManaged<SimulationSystemGroup>()?.Update();
            _testWorld.PopTime();
        }

        #endregion

        #region AI State Transition Tests

        [Test]
        public void AIState_Chase_TransitionsToAttack_WhenInRange()
        {
            // 攻撃範囲2のエンティティをターゲットから距離1の位置に配置
            var entity = CreateEnemyEntity(
                position: new float3(1f, 0f, 0f),
                attackRange: 2f);
            _entityManager.SetComponentData(entity, new ChaseTarget { Position = float3.zero });

            UpdateWorld();

            var aiState = _entityManager.GetComponentData<EnemyAIState>(entity);
            Assert.AreEqual(EcsEnemyAIStateType.Attack, aiState.CurrentState,
                "Chase状態の敵が攻撃範囲内にいる場合、Attack状態に遷移すべき");
        }

        [Test]
        public void AIState_Attack_TransitionsToChase_WhenOutOfRange()
        {
            // Attack状態で攻撃範囲外に配置
            var entity = CreateEnemyEntity(
                position: new float3(10f, 0f, 0f),
                attackRange: 2f,
                attackRangeExitMultiplier: 1.5f,
                initialState: EcsEnemyAIStateType.Attack);
            _entityManager.SetComponentData(entity, new ChaseTarget { Position = float3.zero });

            UpdateWorld();

            var aiState = _entityManager.GetComponentData<EnemyAIState>(entity);
            Assert.AreEqual(EcsEnemyAIStateType.Chase, aiState.CurrentState,
                "Attack状態の敵が攻撃範囲外に出た場合、Chase状態に遷移すべき");
        }

        [Test]
        public void AIState_HitStun_TransitionsToChase_WhenTimerExpires()
        {
            // HitStun状態でタイマー切れ直前に設定（遠距離配置でAttack遷移を防止）
            var entity = CreateEnemyEntity(
                position: new float3(50f, 0f, 0f),
                hitStunDuration: 0.01f);

            _entityManager.SetComponentData(entity, new EnemyAIState
            {
                CurrentState = EcsEnemyAIStateType.HitStun,
                StateTimer = 0.001f // DeltaTime(0.016)で確実に0以下になる
            });

            UpdateWorld();

            var aiState = _entityManager.GetComponentData<EnemyAIState>(entity);
            Assert.AreEqual(EcsEnemyAIStateType.Chase, aiState.CurrentState,
                "HitStun状態のタイマーが切れた場合、Chase状態に復帰すべき");
        }

        [Test]
        public void AIState_Damage_TransitionsToHitStun()
        {
            // 遠距離配置でAttack遷移を防止
            var entity = CreateEnemyEntity(
                position: new float3(50f, 0f, 0f),
                maxHp: 100);

            // 非致死ダメージを設定
            _entityManager.SetComponentData(entity, new DamageEvent
            {
                Damage = 30,
                Knockback = float3.zero
            });

            UpdateWorld();

            var aiState = _entityManager.GetComponentData<EnemyAIState>(entity);
            Assert.AreEqual(EcsEnemyAIStateType.HitStun, aiState.CurrentState,
                "ダメージを受けた敵はHitStun状態に遷移すべき");
        }

        [Test]
        public void AIState_LethalDamage_TransitionsToDead()
        {
            // 遠距離配置でAttack遷移を防止
            var entity = CreateEnemyEntity(
                position: new float3(50f, 0f, 0f),
                maxHp: 50);

            // 致死ダメージを設定
            _entityManager.SetComponentData(entity, new DamageEvent
            {
                Damage = 100,
                Knockback = float3.zero
            });

            UpdateWorld();

            var aiState = _entityManager.GetComponentData<EnemyAIState>(entity);
            Assert.AreEqual(EcsEnemyAIStateType.Dead, aiState.CurrentState,
                "致死ダメージを受けた敵はDead状態に遷移すべき");
        }

        #endregion

        #region Movement Tests

        [Test]
        public void Movement_Chase_MovesTowardsTarget()
        {
            var startPos = new float3(10f, 0f, 0f);
            var entity = CreateEnemyEntity(position: startPos, moveSpeed: 5f, attackRange: 1f);
            _entityManager.SetComponentData(entity, new ChaseTarget { Position = float3.zero });

            UpdateWorld();

            var transform = _entityManager.GetComponentData<LocalTransform>(entity);
            float distAfter = math.distance(transform.Position, float3.zero);
            float distBefore = math.distance(startPos, float3.zero);

            Assert.Less(distAfter, distBefore,
                "Chase状態の敵はターゲットに向かって移動すべき");
        }

        [Test]
        public void Movement_Attack_DoesNotMove()
        {
            var startPos = new float3(1f, 0f, 0f);
            var entity = CreateEnemyEntity(
                position: startPos,
                attackRange: 2f,
                initialState: EcsEnemyAIStateType.Attack);
            _entityManager.SetComponentData(entity, new ChaseTarget { Position = float3.zero });

            UpdateWorld();

            var transform = _entityManager.GetComponentData<LocalTransform>(entity);
            // Attack状態の場合、EnemyMovementSystemはスキップするのでほぼ同じ位置
            Assert.AreEqual(startPos.x, transform.Position.x, 0.01f,
                "Attack状態の敵は移動しないべき");
        }

        [Test]
        public void Movement_Dead_DoesNotMove()
        {
            var startPos = new float3(10f, 0f, 0f);
            var entity = CreateEnemyEntity(position: startPos, maxHp: 10);

            // 致死ダメージで即Dead
            _entityManager.SetComponentData(entity, new DamageEvent { Damage = 100 });
            UpdateWorld();

            var posAfterDeath = _entityManager.GetComponentData<LocalTransform>(entity).Position;

            // もう一度更新しても動かないことを確認
            UpdateWorld();
            var posAfterSecondUpdate = _entityManager.GetComponentData<LocalTransform>(entity).Position;

            Assert.AreEqual(posAfterDeath.x, posAfterSecondUpdate.x, 0.01f,
                "Dead状態の敵は移動しないべき");
        }

        #endregion

        #region Damage Processing Tests

        [Test]
        public void Damage_ReducesHp()
        {
            // 遠距離配置でAttack遷移を防止
            var entity = CreateEnemyEntity(position: new float3(50f, 0f, 0f), maxHp: 100);

            _entityManager.SetComponentData(entity, new DamageEvent
            {
                Damage = 30,
                Knockback = float3.zero
            });

            UpdateWorld();

            var enemyData = _entityManager.GetComponentData<EnemyData>(entity);
            Assert.AreEqual(70, enemyData.CurrentHp, "30ダメージ後のHPは70であるべき");
        }

        [Test]
        public void Damage_LethalDamage_TransitionsToDeadState()
        {
            // 遠距離配置でAttack遷移を防止
            var entity = CreateEnemyEntity(position: new float3(50f, 0f, 0f), maxHp: 50);

            _entityManager.SetComponentData(entity, new DamageEvent
            {
                Damage = 100,
                Knockback = float3.zero
            });

            UpdateWorld();

            var enemyData = _entityManager.GetComponentData<EnemyData>(entity);
            Assert.AreEqual(0, enemyData.CurrentHp, "致死ダメージ後のHPは0であるべき");

            var aiState = _entityManager.GetComponentData<EnemyAIState>(entity);
            Assert.AreEqual(EcsEnemyAIStateType.Dead, aiState.CurrentState,
                "致死ダメージ後にDead状態に遷移すべき");
        }

        [Test]
        public void Damage_NoDamage_Skipped()
        {
            // 遠距離配置でAttack遷移を防止
            var entity = CreateEnemyEntity(position: new float3(50f, 0f, 0f), maxHp: 100);

            // ダメージなし
            _entityManager.SetComponentData(entity, new DamageEvent
            {
                Damage = 0,
                Knockback = float3.zero
            });

            UpdateWorld();

            var enemyData = _entityManager.GetComponentData<EnemyData>(entity);
            Assert.AreEqual(100, enemyData.CurrentHp, "ダメージ0の場合HPは変化しないべき");

            var aiState = _entityManager.GetComponentData<EnemyAIState>(entity);
            Assert.AreEqual(EcsEnemyAIStateType.Chase, aiState.CurrentState,
                "ダメージ0の場合、状態は変化しないべき");
        }

        #endregion

        #region Spawn Position Tests

        [Test]
        public void SpawnPosition_WithinDistanceRange()
        {
            int count = 100;
            float minDist = 10f;
            float maxDist = 20f;
            float3 playerPos = new float3(5f, 0f, 5f);

            var results = new NativeArray<float3>(count, Allocator.TempJob);
            SpawnPositionCalculator.CalculateImmediate(count, playerPos, minDist, maxDist, 42, results);

            for (int i = 0; i < count; i++)
            {
                float distance = math.distance(results[i], playerPos);
                Assert.GreaterOrEqual(distance, minDist - 0.01f,
                    $"スポーン位置[{i}]がminDistance未満");
                Assert.LessOrEqual(distance, maxDist + 0.01f,
                    $"スポーン位置[{i}]がmaxDistanceを超過");
            }

            results.Dispose();
        }

        [Test]
        public void SpawnPosition_BatchGeneration_CorrectCount()
        {
            int count = 500;
            var results = new NativeArray<float3>(count, Allocator.TempJob);
            SpawnPositionCalculator.CalculateImmediate(count, float3.zero, 10f, 20f, 123, results);

            // 全ポジションが非ゼロ（計算が実行された証拠）
            int nonZeroCount = 0;
            for (int i = 0; i < count; i++)
            {
                if (math.lengthsq(results[i]) > 0.01f)
                    nonZeroCount++;
            }

            Assert.AreEqual(count, nonZeroCount, "全スポーン位置が計算されるべき");

            results.Dispose();
        }

        #endregion
    }
}
