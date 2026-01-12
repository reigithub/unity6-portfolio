using MasterMemory;
using MessagePack;

namespace Game.Library.Shared.MasterData.MemoryTables
{
    [MemoryTable("ScoreTimeAttackStageTotalResultMaster"), MessagePackObject(true)]
    public sealed partial class ScoreTimeAttackStageTotalResultMaster
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
