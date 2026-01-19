using UnityEngine;
using UnityEngine.AI;

namespace Game.Shared.Extensions
{
    /// <summary>
    /// NavMeshAgent拡張メソッド
    /// Unity 6のSetDestinationバグ回避用
    /// 参考: https://www.youtube.com/watch?v=-egkBSkF_LA (LlamAcademy)
    /// </summary>
    public static class NavMeshAgentExtensions
    {
        /// <summary>
        /// SetDestinationの代替メソッド（Unity 6 バグ回避）
        /// SetDestinationは毎回velocityを0にリセットするバグがあるため、
        /// CalculatePath + SetPathを使用する
        /// </summary>
        /// <param name="agent">NavMeshAgent</param>
        /// <param name="targetLocation">目標位置</param>
        /// <param name="positionLeniency">位置の許容範囲（0の場合はSamplePositionをスキップ）</param>
        /// <returns>パスが設定できたかどうか</returns>
        public static bool SetDestinationImmediate(
            this NavMeshAgent agent,
            Vector3 targetLocation,
            float positionLeniency = 0)
        {
            NavMeshPath path = new();
            NavMeshQueryFilter queryFilter = new()
            {
                agentTypeID = agent.agentTypeID,
                areaMask = agent.areaMask
            };

            // positionLeniencyが指定されている場合、NavMesh上の有効な位置を探す
            if (positionLeniency != 0)
            {
                if (!NavMesh.SamplePosition(targetLocation, out NavMeshHit hit, positionLeniency, queryFilter))
                {
                    return false;
                }
                targetLocation = hit.position;
            }

            // パスを計算してセット
            bool canSetPath = NavMesh.CalculatePath(
                agent.transform.position,
                targetLocation,
                queryFilter,
                path
            );

            if (canSetPath)
            {
                agent.SetPath(path);
            }

            return canSetPath;
        }
    }
}
