using Cysharp.Threading.Tasks;

namespace Game.Shared.SaveData
{
    /// <summary>
    /// オーディオ設定サービスのインターフェース
    /// </summary>
    public interface IAudioSaveService
    {
        /// <summary>オーディオ設定データ</summary>
        AudioSaveData Data { get; }

        /// <summary>データが読み込まれているか</summary>
        bool IsLoaded { get; }

        /// <summary>セーブデータを読み込む</summary>
        UniTask LoadAsync();

        /// <summary>セーブデータを保存する</summary>
        UniTask SaveAsync();

        /// <summary>変更がある場合のみセーブデータを保存する</summary>
        UniTask SaveIfDirtyAsync();

        /// <summary>マスターボリュームを設定 (0-10)</summary>
        void SetMasterVolume(int value);

        /// <summary>BGMボリュームを設定 (0-10)</summary>
        void SetBgmVolume(int value);

        /// <summary>ボイスボリュームを設定 (0-10)</summary>
        void SetVoiceVolume(int value);

        /// <summary>SEボリュームを設定 (0-10)</summary>
        void SetSeVolume(int value);

        /// <summary>現在の設定をAudioServiceに適用</summary>
        void ApplyToAudioService();
    }
}
