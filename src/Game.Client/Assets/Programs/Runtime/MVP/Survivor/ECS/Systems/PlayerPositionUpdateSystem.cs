using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.MVP.Survivor.ECS
{
    /// <summary>
    /// プレイヤー座標を全ChaseTargetコンポーネントに書き込むシステム
    /// Co-op対応: 複数プレイヤーから最寄りを選択
    /// マネージドシステム（NativeList管理が必要なため）
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial class PlayerPositionUpdateSystem : SystemBase
    {
        /// <summary>プレイヤー座標リスト（外部から毎フレーム設定）</summary>
        public NativeList<float3> PlayerPositions;

        protected override void OnCreate()
        {
            PlayerPositions = new NativeList<float3>(4, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            if (PlayerPositions.IsCreated)
                PlayerPositions.Dispose();
        }

        protected override void OnUpdate()
        {
            if (!PlayerPositions.IsCreated || PlayerPositions.Length == 0)
                return;

            var positions = PlayerPositions.AsArray();

            Entities
                .WithAll<EnemyAliveTag>()
                .WithReadOnly(positions)
                .ForEach((ref ChaseTarget chaseTarget, in LocalTransform transform) =>
                {
                    float3 enemyPos = transform.Position;
                    float bestDistSq = float.MaxValue;
                    float3 bestPos = positions[0];

                    for (int i = 0; i < positions.Length; i++)
                    {
                        float3 delta = positions[i] - enemyPos;
                        delta.y = 0f;
                        float dSq = math.lengthsq(delta);
                        if (dSq < bestDistSq)
                        {
                            bestDistSq = dSq;
                            bestPos = positions[i];
                        }
                    }

                    chaseTarget.Position = bestPos;
                })
                .ScheduleParallel();
        }
    }
}
