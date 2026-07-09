using Game.Shared.Services;
using UnityEngine;

namespace Game.Shared.SaveData
{
    /// <summary>
    /// オーディオ設定セーブサービス
    /// マスター/BGM/ボイス/SEのボリューム設定を永続化
    /// </summary>
    public class AudioSaveService : SaveRepositoryBase<AudioSaveData>, IAudioSaveService
    {
        private const int MaxVolume = 10;
        private const int MinVolume = 0;

        private readonly IAudioService _audioService;

        protected override string SaveKey => "audio_settings";

        public AudioSaveService(ISaveDataStorage storage, IAudioService audioService) : base(storage)
        {
            _audioService = audioService;
        }

        public void SetMasterVolume(int value)
        {
            if (Data == null) return;

            Data.MasterVolume = Mathf.Clamp(value, MinVolume, MaxVolume);
            MarkDirty();
            ApplyToAudioService();
        }

        public void SetBgmVolume(int value)
        {
            if (Data == null) return;

            Data.BgmVolume = Mathf.Clamp(value, MinVolume, MaxVolume);
            MarkDirty();
            ApplyToAudioService();
        }

        public void SetVoiceVolume(int value)
        {
            if (Data == null) return;

            Data.VoiceVolume = Mathf.Clamp(value, MinVolume, MaxVolume);
            MarkDirty();
            ApplyToAudioService();
        }

        public void SetSeVolume(int value)
        {
            if (Data == null) return;

            Data.SeVolume = Mathf.Clamp(value, MinVolume, MaxVolume);
            MarkDirty();
            ApplyToAudioService();
        }

        public void ApplyToAudioService()
        {
            if (Data == null || _audioService == null) return;

            _audioService.SetVolume(Data.MasterVolume, Data.BgmVolume, Data.VoiceVolume, Data.SeVolume);
        }

        protected override AudioSaveData CreateNewData()
        {
            return new AudioSaveData();
        }

        protected override int GetDataVersion(AudioSaveData data)
        {
            return data.Version;
        }

        protected override void MigrateData(AudioSaveData data, int fromVersion)
        {
            // バージョン1からのマイグレーション
            // if (fromVersion < 2) { ... }

            data.Version = CurrentVersion;
            Debug.Log($"[AudioSaveService] Data migrated from version {fromVersion} to {CurrentVersion}");
        }

        protected override void OnDataLoaded(AudioSaveData data)
        {
            // 読み込み後に即座に適用
            ApplyToAudioService();
        }
    }
}
