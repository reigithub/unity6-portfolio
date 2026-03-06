using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.MVP.Survivor.ECS
{
    /// <summary>
    /// 敵の移動をBurst並列で処理するシステム
    /// Chase状態の敵のみターゲットに向かって移動
    /// EnemySteeringResultがあれば操舵結果を使用、なければ直進
    /// </summary>
    [DisableAutoCreation]
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EnemySteeringSystem))]
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
    /// EnemySteeringResultの操舵方向を優先的に使用
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
            in EnemySteeringResult steering,
            in EnemyAliveTag alive)
        {
            // Chase状態の場合のみ移動
            if (aiState.CurrentState != EcsEnemyAIStateType.Chase)
                return;

            float3 direction;

            if (steering.HasObstacle)
            {
                // 操舵結果がある場合はそちらを使用
                direction = steering.SteeringDirection;
            }
            else
            {
                // 直進: ターゲットに向かう
                float3 currentPos = transform.Position;
                float3 targetPos = chaseTarget.Position;

                direction = targetPos - currentPos;
                direction.y = 0f;

                float distanceSq = math.lengthsq(direction);
                if (distanceSq < 0.001f)
                    return;

                direction = math.normalize(direction);
            }

            // 移動
            float3 movement = direction * enemyData.MoveSpeed * DeltaTime;
            transform.Position += movement;

            // ターゲット方向を向く
            if (math.lengthsq(direction) > 0.001f)
            {
                quaternion targetRotation = quaternion.LookRotationSafe(direction, math.up());
                transform.Rotation = math.slerp(transform.Rotation, targetRotation, enemyData.RotationSpeed * DeltaTime);
            }
        }
    }
}
