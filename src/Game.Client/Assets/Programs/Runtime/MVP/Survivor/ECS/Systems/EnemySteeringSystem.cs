using Game.Shared.Constants;
using Unity.Collections;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Game.MVP.Survivor.ECS
{
    /// <summary>
    /// RaycastCommandベースの障害物回避 + セパレーション力
    /// Chase状態の敵のみ処理。結果をEnemySteeringResultに書き込み、
    /// EnemyMovementSystemが読み取る。
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EnemyAIStateSystem))]
    [UpdateBefore(typeof(EnemyMovementSystem))]
    public partial class EnemySteeringSystem : SystemBase
    {
        private const float LookaheadDistance = 1.5f;
        private const float SteerAngleRad = math.PI / 3f; // 60°
        private const float SeparationRadius = 1.2f;
        private const float SeparationRadiusSq = SeparationRadius * SeparationRadius;
        private const float SeparationStrength = 0.5f;

        private int _obstacleLayerMask;

        protected override void OnCreate()
        {
            _obstacleLayerMask = LayerMaskConstants.Structure;
        }

        protected override void OnUpdate()
        {
            // Chase状態の生存敵のみ対象
            var query = GetEntityQuery(
                ComponentType.ReadOnly<EnemyAIState>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<ChaseTarget>(),
                ComponentType.ReadOnly<EnemyData>(),
                ComponentType.ReadWrite<EnemySteeringResult>(),
                ComponentType.ReadOnly<EnemyAliveTag>());

            int count = query.CalculateEntityCount();
            if (count == 0) return;

            // 全生存敵の位置スナップショット（セパレーション用）
            var allPositions = query.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

            // Chase状態のインデックスとデータを収集
            var chaseEntities = new NativeList<int>(count, Allocator.TempJob);
            var chaseDirections = new NativeArray<float3>(count, Allocator.TempJob);
            var chasePositions = new NativeArray<float3>(count, Allocator.TempJob);

            var aiStates = query.ToComponentDataArray<EnemyAIState>(Allocator.TempJob);
            var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            var chaseTargets = query.ToComponentDataArray<ChaseTarget>(Allocator.TempJob);

            // Chase状態のエンティティのインデックスと方向を収集
            for (int i = 0; i < count; i++)
            {
                if (aiStates[i].CurrentState == EcsEnemyAIStateType.Chase)
                {
                    float3 dir = chaseTargets[i].Position - transforms[i].Position;
                    dir.y = 0f;
                    float lenSq = math.lengthsq(dir);
                    if (lenSq > 0.001f)
                    {
                        chaseDirections[chaseEntities.Length] = math.normalize(dir);
                        chasePositions[chaseEntities.Length] = transforms[i].Position;
                        chaseEntities.Add(i);
                    }
                }
            }

            int chaseCount = chaseEntities.Length;

            if (chaseCount > 0)
            {
                // RaycastCommandバッチ生成
                var rayCommands = new NativeArray<RaycastCommand>(chaseCount, Allocator.TempJob);
                var rayResults = new NativeArray<RaycastHit>(chaseCount, Allocator.TempJob);

                var queryParams = new QueryParameters(_obstacleLayerMask, false, QueryTriggerInteraction.Ignore, false);

                for (int i = 0; i < chaseCount; i++)
                {
                    var origin = chasePositions[i];
                    origin.y += 0.5f; // 地面から少し上
                    rayCommands[i] = new RaycastCommand(origin, chaseDirections[i], queryParams, LookaheadDistance);
                }

                // レイキャスト実行
                var raycastHandle = RaycastCommand.ScheduleBatch(rayCommands, rayResults, 32, 1, default);
                raycastHandle.Complete();

                // セパレーション + レイキャスト結果を EnemySteeringResult に書き込み
                var steeringResults = query.ToComponentDataArray<EnemySteeringResult>(Allocator.TempJob);

                // 全エンティティのsteeringをリセット
                for (int i = 0; i < count; i++)
                {
                    steeringResults[i] = default;
                }

                for (int ci = 0; ci < chaseCount; ci++)
                {
                    int entityIdx = chaseEntities[ci];
                    float3 currentDir = chaseDirections[ci];
                    float3 currentPos = chasePositions[ci];
                    bool hasObstacle = false;
                    float3 steeringDir = currentDir;

                    // レイキャスト結果: 壁があれば方向を偏向
                    if (rayResults[ci].colliderInstanceID != 0)
                    {
                        hasObstacle = true;
                        float3 hitNormal = rayResults[ci].normal;
                        hitNormal.y = 0f;
                        hitNormal = math.normalizesafe(hitNormal);

                        // 法線方向に偏向: 進行方向と法線のクロス積で左右を判定
                        float cross = currentDir.x * hitNormal.z - currentDir.z * hitNormal.x;
                        float steerSign = cross >= 0f ? 1f : -1f;

                        // 60°回転
                        float sinA = math.sin(SteerAngleRad * steerSign);
                        float cosA = math.cos(SteerAngleRad * steerSign);
                        steeringDir = new float3(
                            currentDir.x * cosA - currentDir.z * sinA,
                            0f,
                            currentDir.x * sinA + currentDir.z * cosA
                        );
                    }

                    // セパレーション力: 近傍の敵から離れる方向に微小力を加算
                    float3 separation = float3.zero;
                    for (int j = 0; j < count; j++)
                    {
                        if (j == entityIdx) continue;
                        float3 diff = currentPos - allPositions[j].Position;
                        diff.y = 0f;
                        float distSq = math.lengthsq(diff);
                        if (distSq > 0.01f && distSq < SeparationRadiusSq)
                        {
                            separation += math.normalize(diff) * (1f - distSq / SeparationRadiusSq);
                        }
                    }

                    if (math.lengthsq(separation) > 0.001f)
                    {
                        steeringDir = math.normalizesafe(steeringDir + separation * SeparationStrength);
                        hasObstacle = true; // セパレーションも操舵扱い
                    }
                    else if (hasObstacle)
                    {
                        steeringDir = math.normalizesafe(steeringDir);
                    }

                    steeringResults[entityIdx] = new EnemySteeringResult
                    {
                        SteeringDirection = steeringDir,
                        HasObstacle = hasObstacle
                    };
                }

                query.CopyFromComponentDataArray(steeringResults);

                steeringResults.Dispose();
                rayCommands.Dispose();
                rayResults.Dispose();
            }
            else
            {
                // Chase状態の敵がいない場合、全てリセット
                var steeringResults = query.ToComponentDataArray<EnemySteeringResult>(Allocator.TempJob);
                for (int i = 0; i < count; i++)
                {
                    steeringResults[i] = default;
                }
                query.CopyFromComponentDataArray(steeringResults);
                steeringResults.Dispose();
            }

            chaseEntities.Dispose();
            chaseDirections.Dispose();
            chasePositions.Dispose();
            aiStates.Dispose();
            transforms.Dispose();
            chaseTargets.Dispose();
            allPositions.Dispose();
        }
    }
}
