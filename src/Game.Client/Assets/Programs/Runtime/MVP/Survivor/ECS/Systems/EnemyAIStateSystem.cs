using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.MVP.Survivor.ECS
{
    /// <summary>
    /// 敵AI状態遷移をBurst並列で処理するシステム
    /// 距離判定に基づいてChase/Attack/HitStun/Dead間の遷移を管理
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct EnemyAIStateSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            new UpdateAIStateJob
            {
                DeltaTime = deltaTime
            }.ScheduleParallel();
        }
    }

    /// <summary>
    /// AI状態遷移Job
    /// </summary>
    [BurstCompile]
    public partial struct UpdateAIStateJob : IJobEntity
    {
        public float DeltaTime;

        public void Execute(
            ref EnemyAIState aiState,
            in LocalTransform transform,
            in EnemyData enemyData,
            in ChaseTarget chaseTarget,
            in EnemyAliveTag alive)
        {
            // Dead状態は何もしない
            if (aiState.CurrentState == EcsEnemyAIStateType.Dead)
                return;

            // ターゲットとの距離を計算
            float3 toTarget = chaseTarget.Position - transform.Position;
            toTarget.y = 0f;
            float distanceSq = math.lengthsq(toTarget);
            float attackRangeSq = enemyData.AttackRange * enemyData.AttackRange;

            switch (aiState.CurrentState)
            {
                case EcsEnemyAIStateType.Chase:
                    // 攻撃範囲内に入ったらAttackへ遷移
                    if (distanceSq <= attackRangeSq)
                    {
                        aiState.CurrentState = EcsEnemyAIStateType.Attack;
                        aiState.StateTimer = 0f;
                    }
                    break;

                case EcsEnemyAIStateType.Attack:
                    // 攻撃タイマー更新
                    aiState.StateTimer -= DeltaTime;

                    // 攻撃範囲外に出たらChaseへ遷移
                    float exitRange = enemyData.AttackRange * enemyData.AttackRangeExitMultiplier;
                    float exitRangeSq = exitRange * exitRange;
                    if (distanceSq > exitRangeSq)
                    {
                        aiState.CurrentState = EcsEnemyAIStateType.Chase;
                        aiState.StateTimer = 0f;
                    }
                    else if (aiState.StateTimer <= 0f)
                    {
                        // 攻撃クールダウンリセット（実際のダメージ適用はDamageSystemに委譲）
                        aiState.StateTimer = enemyData.AttackCooldown;
                    }
                    break;

                case EcsEnemyAIStateType.HitStun:
                    // ヒットスタンタイマー更新
                    aiState.StateTimer -= DeltaTime;
                    if (aiState.StateTimer <= 0f)
                    {
                        // Chase状態に復帰
                        aiState.CurrentState = EcsEnemyAIStateType.Chase;
                        aiState.StateTimer = 0f;
                    }
                    break;
            }
        }
    }
}
