using Unity.Entities;
using Unity.Mathematics;

namespace Game.MVP.Survivor.ECS
{
    /// <summary>
    /// スポーン設定シングルトン
    /// </summary>
    public struct SpawnConfig : IComponentData
    {
        /// <summary>プレイヤーのワールド座標</summary>
        public float3 PlayerPosition;

        /// <summary>最小スポーン距離</summary>
        public float MinSpawnDistance;

        /// <summary>最大スポーン距離</summary>
        public float MaxSpawnDistance;

        /// <summary>乱数シード</summary>
        public uint RandomSeed;
    }
}
