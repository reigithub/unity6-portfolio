using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.MVP.Survivor.ECS
{
    /// <summary>
    /// 敵の移動をBurst並列で処理するシステム
    /// Chase状態の敵のみターゲットに向かって直進移動
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EnemyAIStateSystem))]
    public partial struct EnemyMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            new EnemyChaseJob
            {
                DeltaTime = deltaTime
            }.ScheduleParallel();
        }
    }

    /// <summary>
    /// 敵の追尾移動Job
    /// Chase状態の敵をターゲットに向かって移動させる
    /// </summary>
    [BurstCompile]
    public partial struct EnemyChaseJob : IJobEntity
    {
        public float DeltaTime;

        public void Execute(
            ref LocalTransform transform,
            in EnemyData enemyData,
            in EnemyAIState aiState,
            in ChaseTarget chaseTarget,
            in EnemyAliveTag alive)
        {
            // Chase状態の場合のみ移動
            if (aiState.CurrentState != EcsEnemyAIStateType.Chase)
                return;

            float3 currentPos = transform.Position;
            float3 targetPos = chaseTarget.Position;

            // Y軸は無視（水平面での追尾）
            float3 direction = targetPos - currentPos;
            direction.y = 0f;

            float distanceSq = math.lengthsq(direction);
            if (distanceSq < 0.001f)
                return;

            // 正規化して移動
            direction = math.normalize(direction);
            float3 movement = direction * enemyData.MoveSpeed * DeltaTime;
            transform.Position += movement;

            // ターゲット方向を向く
            quaternion targetRotation = quaternion.LookRotationSafe(direction, math.up());
            transform.Rotation = math.slerp(transform.Rotation, targetRotation, enemyData.RotationSpeed * DeltaTime);
        }
    }
}
