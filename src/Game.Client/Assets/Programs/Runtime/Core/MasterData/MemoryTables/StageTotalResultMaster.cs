using MasterMemory;
using MessagePack;

namespace Game.Core.MasterData.MemoryTables
{
    [MemoryTable("StageTotalResultMaster"), MessagePackObject(true)]
    public sealed partial class StageTotalResultMaster
    {
        [PrimaryKey]
        public int Id { get; set; }

        public int TotalScore { get; set; }

        public string TotalRank { get; set; }

        public string AnimatorStateName { get; set; }

        public int BgmAudioId { get; set; }
        public int VoiceAudioId { get; set; }
        public int SoundEffectAudioId { get; set; }
    }
}