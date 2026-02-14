using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.MVP.Survivor.ECS
{
    /// <summary>
    /// スポーン位置をBurst並列Jobで一括計算するシステム
    /// N個のスポーン位置をプレイヤー周囲の円環上にランダム生成
    /// </summary>
    [BurstCompile]
    public struct CalculateSpawnPositionsJob : IJobParallelFor
    {
        /// <summary>プレイヤーのワールド座標</summary>
        [ReadOnly] public float3 PlayerPosition;

        /// <summary>最小スポーン距離</summary>
        [ReadOnly] public float MinDistance;

        /// <summary>最大スポーン距離</summary>
        [ReadOnly] public float MaxDistance;

        /// <summary>ベースシード</summary>
        [ReadOnly] public uint BaseSeed;

        /// <summary>計算結果の書き込み先</summary>
        [WriteOnly] public NativeArray<float3> Results;

        public void Execute(int index)
        {
            // インデックスごとに異なるシードで乱数生成
            var random = new Random(BaseSeed + (uint)index + 1);

            float angle = random.NextFloat(0f, math.PI * 2f);
            float distance = random.NextFloat(MinDistance, MaxDistance);

            float3 offset = new float3(
                math.cos(angle) * distance,
                0f,
                math.sin(angle) * distance
            );

            Results[index] = PlayerPosition + offset;
        }
    }

    /// <summary>
    /// スポーン位置計算のユーティリティ
    /// テストおよびブリッジからの直接呼び出し用
    /// </summary>
    public static class SpawnPositionCalculator
    {
        /// <summary>
        /// N個のスポーン位置をBurst並列Jobで計算
        /// </summary>
        /// <param name="count">計算するスポーン数</param>
        /// <param name="playerPosition">プレイヤー座標</param>
        /// <param name="minDistance">最小距離</param>
        /// <param name="maxDistance">最大距離</param>
        /// <param name="seed">乱数シード</param>
        /// <param name="results">結果を格納するNativeArray（呼び出し側が確保・解放）</param>
        /// <returns>完了可能なJobHandle</returns>
        public static JobHandle ScheduleCalculation(
            int count,
            float3 playerPosition,
            float minDistance,
            float maxDistance,
            uint seed,
            NativeArray<float3> results)
        {
            var job = new CalculateSpawnPositionsJob
            {
                PlayerPosition = playerPosition,
                MinDistance = minDistance,
                MaxDistance = maxDistance,
                BaseSeed = seed,
                Results = results
            };

            return job.Schedule(count, 64);
        }

        /// <summary>
        /// 同期版：即座に計算を完了して結果を返す
        /// </summary>
        public static void CalculateImmediate(
            int count,
            float3 playerPosition,
            float minDistance,
            float maxDistance,
            uint seed,
            NativeArray<float3> results)
        {
            var handle = ScheduleCalculation(count, playerPosition, minDistance, maxDistance, seed, results);
            handle.Complete();
        }
    }
}
