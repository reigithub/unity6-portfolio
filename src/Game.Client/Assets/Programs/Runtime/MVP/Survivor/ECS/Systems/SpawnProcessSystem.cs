using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.MVP.Survivor.ECS
{
    /// <summary>
    /// EnemySpawnRequestを消費してECSエンティティを生成するシステム
    /// GameObjectプールとの連携はBridge経由で行う
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(PlayerPositionUpdateSystem))]
    public partial class SpawnProcessSystem : SystemBase
    {
        /// <summary>
        /// 外部からスポーンリクエストを追加するためのコールバック
        /// Bridge経由で設定される
        /// </summary>
        public delegate void SpawnCallback(Entity entity, EnemySpawnRequest request);

        /// <summary>スポーン完了時のコールバック</summary>
        public SpawnCallback OnEntitySpawned;

        private EntityArchetype _enemyArchetype;
        private readonly List<(Entity entity, EnemySpawnRequest request)> _pendingCallbacks = new();

        protected override void OnCreate()
        {
            _enemyArchetype = EntityManager.CreateArchetype(
                typeof(EnemyData),
                typeof(EnemyAIState),
                typeof(ChaseTarget),
                typeof(DamageEvent),
                typeof(EnemyAliveTag),
                typeof(EnemyDeadTag),
                typeof(LocalTransform),
                typeof(EnemySteeringResult),
                typeof(ManagedGameObjectReference)
            );
        }

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (request, entity) in SystemAPI.Query<RefRO<EnemySpawnRequest>>().WithEntityAccess())
            {
                var spawnRequest = request.ValueRO;

                // エンティティ生成
                var newEntity = ecb.CreateEntity(_enemyArchetype);

                ecb.SetComponent(newEntity, new EnemyData
                {
                    EnemyId = spawnRequest.EnemyId,
                    EnemyType = spawnRequest.EnemyType,
                    CurrentHp = spawnRequest.MaxHp,
                    MaxHp = spawnRequest.MaxHp,
                    AttackDamage = spawnRequest.AttackDamage,
                    MoveSpeed = spawnRequest.MoveSpeed,
                    AttackRange = spawnRequest.AttackRange,
                    AttackCooldown = spawnRequest.AttackCooldown,
                    HitStunDuration = spawnRequest.HitStunDuration,
                    RotationSpeed = spawnRequest.RotationSpeed,
                    DeathAnimDuration = spawnRequest.DeathAnimDuration,
                    AttackRangeExitMultiplier = spawnRequest.AttackRangeExitMultiplier,
                    ExperienceValue = spawnRequest.ExperienceValue,
                    ItemDropGroupId = spawnRequest.ItemDropGroupId,
                    ExpDropGroupId = spawnRequest.ExpDropGroupId
                });

                ecb.SetComponent(newEntity, new EnemyAIState
                {
                    CurrentState = EcsEnemyAIStateType.Chase,
                    StateTimer = 0f
                });

                ecb.SetComponent(newEntity, new ChaseTarget
                {
                    Position = new float3(float.MaxValue, 0, float.MaxValue)
                });

                ecb.SetComponent(newEntity, new DamageEvent
                {
                    Damage = 0,
                    Knockback = float3.zero
                });

                ecb.SetComponent(newEntity, LocalTransform.FromPosition(spawnRequest.Position));

                // EnemyDeadTagは初期状態で無効
                ecb.SetComponentEnabled<EnemyDeadTag>(newEntity, false);

                // リクエストエンティティは消費（破棄）
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();

            // コールバック通知（スポーン完了後にGameObject生成をBridgeに委譲）
            // Query中は構造変更不可のため、先にリストに収集してからコールバックを呼ぶ
            var spawnCallback = OnEntitySpawned;
            if (spawnCallback != null)
            {
                _pendingCallbacks.Clear();
                foreach (var (enemyData, transform, managedRef, entity) in
                    SystemAPI.Query<RefRO<EnemyData>, RefRO<LocalTransform>, ManagedGameObjectReference>()
                        .WithAll<EnemyAliveTag>()
                        .WithEntityAccess())
                {
                    if (managedRef?.GameObject != null)
                        continue;

                    _pendingCallbacks.Add((entity, new EnemySpawnRequest
                    {
                        EnemyId = enemyData.ValueRO.EnemyId,
                        Position = transform.ValueRO.Position,
                        MaxHp = enemyData.ValueRO.MaxHp,
                        AttackDamage = enemyData.ValueRO.AttackDamage,
                        MoveSpeed = enemyData.ValueRO.MoveSpeed,
                        AttackRange = enemyData.ValueRO.AttackRange,
                        AttackCooldown = enemyData.ValueRO.AttackCooldown,
                        HitStunDuration = enemyData.ValueRO.HitStunDuration,
                        RotationSpeed = enemyData.ValueRO.RotationSpeed,
                        DeathAnimDuration = enemyData.ValueRO.DeathAnimDuration,
                        AttackRangeExitMultiplier = enemyData.ValueRO.AttackRangeExitMultiplier,
                        ExperienceValue = enemyData.ValueRO.ExperienceValue,
                        EnemyType = enemyData.ValueRO.EnemyType,
                        ItemDropGroupId = enemyData.ValueRO.ItemDropGroupId,
                        ExpDropGroupId = enemyData.ValueRO.ExpDropGroupId
                    }));
                }

                foreach (var (entity, request) in _pendingCallbacks)
                {
                    spawnCallback.Invoke(entity, request);
                }
            }
        }
    }
}
