using Unity.Entities;
using Unity.Mathematics;

namespace Game.MVP.Survivor.ECS
{
    /// <summary>
    /// プレイヤーのTransform座標を全ChaseTargetコンポーネントに書き込むシステム
    /// マネージドシステム（プレイヤーのTransform参照が必要なため）
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial class PlayerPositionUpdateSystem : SystemBase
    {
        /// <summary>プレイヤー座標（外部から毎フレーム設定）</summary>
        public float3 PlayerPosition;

        /// <summary>座標が有効かどうか</summary>
        public bool IsActive;

        protected override void OnUpdate()
        {
            if (!IsActive)
                return;

            float3 playerPos = PlayerPosition;

            Entities
                .WithAll<EnemyAliveTag>()
                .ForEach((ref ChaseTarget chaseTarget) =>
                {
                    chaseTarget.Position = playerPos;
                })
                .ScheduleParallel();
        }
    }
}
