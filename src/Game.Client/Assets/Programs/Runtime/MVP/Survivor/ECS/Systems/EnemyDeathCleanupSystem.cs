using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.MVP.Survivor.ECS
{
    /// <summary>
    /// 死亡した敵のデータ
    /// </summary>
    public struct EnemyDeathInfo
    {
        public Entity Entity;
        public float3 Position;
        public int EnemyType;
        public int ItemDropGroupId;
        public int ExpDropGroupId;
        public float DeathAnimDuration;
    }

    /// <summary>
    /// EnemyDeadTagが付与されたエンティティを検出し、Bridge経由で通知後にエンティティを破棄するシステム
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial class EnemyDeathCleanupSystem : SystemBase
    {
        /// <summary>死亡通知コールバック</summary>
        public Action<EnemyDeathInfo> OnEnemyDied;

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var deathCallback = OnEnemyDied;

            foreach (var (enemyData, transform, aiState, entity) in
                SystemAPI.Query<RefRO<EnemyData>, RefRO<LocalTransform>, RefRO<EnemyAIState>>()
                    .WithEntityAccess())
            {
                // Dead状態のエンティティのみ処理
                if (aiState.ValueRO.CurrentState != EcsEnemyAIStateType.Dead)
                    continue;

                // 死亡情報を通知
                UnityEngine.Debug.Log($"[EnemyDeathCleanup] Entity {entity.Index} died: HP={enemyData.ValueRO.CurrentHp}/{enemyData.ValueRO.MaxHp}, type={enemyData.ValueRO.EnemyType}, pos={transform.ValueRO.Position}");
                deathCallback?.Invoke(new EnemyDeathInfo
                {
                    Entity = entity,
                    Position = transform.ValueRO.Position,
                    EnemyType = enemyData.ValueRO.EnemyType,
                    ItemDropGroupId = enemyData.ValueRO.ItemDropGroupId,
                    ExpDropGroupId = enemyData.ValueRO.ExpDropGroupId,
                    DeathAnimDuration = enemyData.ValueRO.DeathAnimDuration
                });

                // エンティティ破棄
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
