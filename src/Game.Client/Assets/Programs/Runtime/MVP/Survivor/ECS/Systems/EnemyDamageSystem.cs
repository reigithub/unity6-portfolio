using Unity.Burst;
using Unity.Entities;

namespace Game.MVP.Survivor.ECS
{
    /// <summary>
    /// 敵へのダメージ処理をBurst並列で実行するシステム
    /// DamageEventコンポーネントの値を消費してHPを減算、致死時はDead状態に遷移
    /// タグの切り替え・Entity破棄はEnemyDeathCleanupSystemが担当
    /// </summary>
    [DisableAutoCreation]
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EnemyAIStateSystem))]
    public partial struct EnemyDamageSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new ProcessDamageJob().ScheduleParallel();
        }
    }

    /// <summary>
    /// ダメージ処理Job
    /// </summary>
    [BurstCompile]
    public partial struct ProcessDamageJob : IJobEntity
    {
        public void Execute(
            ref EnemyData enemyData,
            ref EnemyAIState aiState,
            ref DamageEvent damageEvent)
        {
            // 未ダメージの場合はスキップ
            if (damageEvent.Damage <= 0)
                return;

            // Dead状態は処理しない
            if (aiState.CurrentState == EcsEnemyAIStateType.Dead)
            {
                damageEvent.Damage = 0;
                return;
            }

            // HP減算
            enemyData.CurrentHp -= damageEvent.Damage;

            if (enemyData.CurrentHp <= 0)
            {
                // 致死ダメージ → Dead状態に遷移
                enemyData.CurrentHp = 0;
                aiState.CurrentState = EcsEnemyAIStateType.Dead;
                aiState.StateTimer = 0f;
            }
            else
            {
                // HitStun状態に遷移
                aiState.CurrentState = EcsEnemyAIStateType.HitStun;
                aiState.StateTimer = enemyData.HitStunDuration;
            }

            // ダメージイベント消費済み
            damageEvent.Damage = 0;
            damageEvent.Knockback = default;
        }
    }
}
