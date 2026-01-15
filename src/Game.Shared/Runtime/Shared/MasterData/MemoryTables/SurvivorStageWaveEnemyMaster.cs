using MasterMemory;
using MessagePack;

namespace Game.Library.Shared.MasterData.MemoryTables
{
    /// <summary>
    /// Survivorステージウェーブ敵マスター
    /// 各ウェーブでスポーンする敵の設定を定義
    /// </summary>
    [MemoryTable("SurvivorStageWaveEnemyMaster"), MessagePackObject(true)]
    public sealed partial class SurvivorStageWaveEnemyMaster
    {
        [PrimaryKey]
        public int Id { get; set; }

        /// <summary>ウェーブID</summary>
        [SecondaryKey(0), NonUnique]
        public int WaveId { get; set; }

        /// <summary>敵ID</summary>
        public int EnemyId { get; set; }

        /// <summary>スポーン数</summary>
        public int SpawnCount { get; set; }

        /// <summary>スポーン間隔（ミリ秒）</summary>
        public int SpawnInterval { get; set; }

        /// <summary>スポーン開始遅延（ミリ秒）</summary>
        public int SpawnDelay { get; set; }

        /// <summary>最小スポーン距離</summary>
        public int MinSpawnDistance { get; set; }

        /// <summary>最大スポーン距離</summary>
        public int MaxSpawnDistance { get; set; }
    }
}
