using System.Collections.Generic;
using MemoryPack;

namespace Game.Horror.SaveData
{
    [MemoryPackable]
    public partial class HorrorEnemySaveData
    {
        /// <summary>撃破済みスポーンエントリの Id 集合（HorrorEnemySpawnMaster の Id）</summary>
        public List<int> DefeatedSpawnIds { get; set; } = new();

        /// <summary>発火済みスポーントリガーの Id 集合（HorrorEnemySpawnTriggerMaster の Id）。MemoryPack 互換のため末尾追加</summary>
        public List<int> FiredTriggerIds { get; set; } = new();
    }
}
