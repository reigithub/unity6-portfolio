using Game.Core.Services;
using Game.Shared.Enums;
using Game.Shared.SaveData;
using UnityEngine;

namespace Game.Horror.SaveData
{
    /// <summary>
    /// Horror オプション設定のセーブサービス。<see cref="SaveServiceBase{TData}"/> を継承し、
    /// 各設定の値保持・ダーティ管理・永続化を担う。ゲームへの実適用（Screen/QualitySettings 等）は持たない。
    /// 生成・ロード済みインスタンスを GameServiceManager.Register で共有登録して使う（IGameService）。
    /// </summary>
    public class HorrorOptionSaveService : SaveServiceBase<HorrorOptionSaveData>, IGameService
    {
        protected override string SaveKey => "horror_option_settings";
        protected override int CurrentVersion => 1;

        public HorrorOptionSaveService(ISaveDataStorage storage) : base(storage)
        {
        }

        #region Gameplay

        public void SetLanguageCode(string code)
        {
            if (Data == null) return;
            Data.LanguageCode = code;
            MarkDirty();
        }

        public void SetCameraControlHorizontal(bool invert)
        {
            if (Data == null) return;
            Data.CameraControlHorizontal = invert;
            MarkDirty();
        }

        public void SetCameraControlVertical(bool invert)
        {
            if (Data == null) return;
            Data.CameraControlVertical = invert;
            MarkDirty();
        }

        public void SetCameraSensitivityHorizontal(float value)
        {
            if (Data == null) return;
            Data.CameraSensitivityHorizontal = value;
            MarkDirty();
        }

        public void SetCameraSensitivityVertical(float value)
        {
            if (Data == null) return;
            Data.CameraSensitivityVertical = value;
            MarkDirty();
        }

        public void SetCameraAcceleration(float value)
        {
            if (Data == null) return;
            Data.CameraAcceleration = value;
            MarkDirty();
        }

        public void SetCameraShake(float value)
        {
            if (Data == null) return;
            Data.CameraShake = value;
            MarkDirty();
        }

        public void SetCameraFov(float fov)
        {
            if (Data == null) return;
            Data.CameraFov = fov;
            MarkDirty();
        }

        /// <summary>しゃがみ入力モードを保持する（false=ホールド, true=トグル）。</summary>
        public void SetCrouchToggle(bool toggle)
        {
            if (Data == null) return;
            Data.CrouchToggle = toggle;
            MarkDirty();
        }

        #endregion

        #region Graphics

        public void SetDisplayMode(FullScreenMode mode)
        {
            if (Data == null) return;
            Data.DisplayMode = mode;
            MarkDirty();
        }

        public void SetResolution(int width, int height)
        {
            if (Data == null) return;
            Data.ResolutionWidth = width;
            Data.ResolutionHeight = height;
            MarkDirty();
        }

        public void SetFrameRateLimit(int fps)
        {
            if (Data == null) return;
            Data.FrameRateLimit = fps;
            MarkDirty();
        }

        public void SetUncappedFrameRate(bool uncapped)
        {
            if (Data == null) return;
            Data.UncappedFrameRate = uncapped;
            MarkDirty();
        }

        public void SetVSync(bool enabled)
        {
            if (Data == null) return;
            Data.VSync = enabled;
            MarkDirty();
        }

        #endregion

        #region Controls

        /// <summary>
        /// キーリバインドのオーバーライド JSON を保持する（InputActionAsset.SaveBindingOverridesAsJson の出力）。
        /// null は空文字として扱う（オーバーライド無し）。
        /// </summary>
        public void SetInputBindingOverrides(string json)
        {
            if (Data == null) return;
            Data.InputBindingOverridesJson = json ?? "";
            MarkDirty();
        }

        #endregion

        #region Audio

        public void SetMasterVolume(float value)
        {
            if (Data == null) return;
            Data.MasterVolume = Mathf.Clamp01(value);
            MarkDirty();
        }

        public void SetBgmVolume(float value)
        {
            if (Data == null) return;
            Data.BgmVolume = Mathf.Clamp01(value);
            MarkDirty();
        }

        public void SetVoiceVolume(float value)
        {
            if (Data == null) return;
            Data.VoiceVolume = Mathf.Clamp01(value);
            MarkDirty();
        }

        public void SetSeVolume(float value)
        {
            if (Data == null) return;
            Data.SeVolume = Mathf.Clamp01(value);
            MarkDirty();
        }

        #endregion

        protected override int GetDataVersion(HorrorOptionSaveData data) => data.Version;

        protected override void MigrateData(HorrorOptionSaveData data, int fromVersion)
        {
            data.Version = CurrentVersion;
            Debug.Log($"[HorrorOptionSaveService] Migrated from version {fromVersion} to {CurrentVersion}");
        }
    }
}
