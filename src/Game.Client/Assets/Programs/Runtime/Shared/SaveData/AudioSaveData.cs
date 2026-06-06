using MemoryPack;

namespace Game.Shared.SaveData
{
    /// <summary>
    /// オーディオ設定のセーブデータ
    /// 各ボリュームは0-10の10段階
    /// </summary>
    [MemoryPackable]
    public partial class AudioSaveData
    {
        public int Version { get; set; } = 1;

        /// <summary>マスターボリューム (0-10)</summary>
        public int MasterVolume { get; set; } = 5;

        /// <summary>BGMボリューム (0-10)</summary>
        public int BgmVolume { get; set; } = 3;

        /// <summary>ボイスボリューム (0-10)</summary>
        public int VoiceVolume { get; set; } = 7;

        /// <summary>SEボリューム (0-10)</summary>
        public int SeVolume { get; set; } = 5;
    }
}
