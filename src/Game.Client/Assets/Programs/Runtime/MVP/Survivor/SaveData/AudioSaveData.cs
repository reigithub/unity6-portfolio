using MemoryPack;

namespace Game.MVP.Survivor.SaveData
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
        public int MasterVolume { get; set; } = 7;

        /// <summary>BGMボリューム (0-10)</summary>
        public int BgmVolume { get; set; } = 7;

        /// <summary>ボイスボリューム (0-10)</summary>
        public int VoiceVolume { get; set; } = 10;

        /// <summary>SEボリューム (0-10)</summary>
        public int SeVolume { get; set; } = 7;
    }
}
