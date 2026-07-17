using Game.Horror.Services.Interfaces;
using UnityEngine;

namespace Game.Horror.Services
{
    /// <summary>
    /// Horror オプション設定のビジネスロジックを扱うドメインサービス。
    /// 値のクランプ・正規化・Dirty 化を担う。永続化（読み込み・保存）は <see cref="IHorrorOptionSaveRepository"/> に委譲する。
    /// </summary>
    public class HorrorOptionService : IHorrorOptionService
    {
        private readonly IHorrorOptionSaveRepository _repository;

        public HorrorOptionService(IHorrorOptionSaveRepository repository)
        {
            _repository = repository;
        }

        #region Gameplay

        /// <summary>表示言語コードを設定する。</summary>
        public void SetLanguageCode(string code)
        {
            if (_repository.Data == null) return;
            _repository.Data.LanguageCode = code;
            _repository.MarkDirty();
        }

        /// <summary>カメラの水平軸反転設定を保持する。</summary>
        public void SetCameraControlHorizontal(bool invert)
        {
            if (_repository.Data == null) return;
            _repository.Data.CameraControlHorizontal = invert;
            _repository.MarkDirty();
        }

        /// <summary>カメラの垂直軸反転設定を保持する。</summary>
        public void SetCameraControlVertical(bool invert)
        {
            if (_repository.Data == null) return;
            _repository.Data.CameraControlVertical = invert;
            _repository.MarkDirty();
        }

        /// <summary>カメラの水平感度を設定する。</summary>
        public void SetCameraSensitivityHorizontal(float value)
        {
            if (_repository.Data == null) return;
            _repository.Data.CameraSensitivityHorizontal = value;
            _repository.MarkDirty();
        }

        /// <summary>カメラの垂直感度を設定する。</summary>
        public void SetCameraSensitivityVertical(float value)
        {
            if (_repository.Data == null) return;
            _repository.Data.CameraSensitivityVertical = value;
            _repository.MarkDirty();
        }

        /// <summary>カメラの加速度を設定する。</summary>
        public void SetCameraAcceleration(float value)
        {
            if (_repository.Data == null) return;
            _repository.Data.CameraAcceleration = value;
            _repository.MarkDirty();
        }

        /// <summary>カメラの揺れ強度を設定する。</summary>
        public void SetCameraShake(float value)
        {
            if (_repository.Data == null) return;
            _repository.Data.CameraShake = value;
            _repository.MarkDirty();
        }

        /// <summary>カメラの視野角（FOV）を設定する。</summary>
        public void SetCameraFov(float fov)
        {
            if (_repository.Data == null) return;
            _repository.Data.CameraFov = fov;
            _repository.MarkDirty();
        }

        /// <summary>走り入力モードを保持する（false=ホールド, true=トグル）。</summary>
        public void SetSprintToggle(bool toggle)
        {
            if (_repository.Data == null) return;
            _repository.Data.SprintToggle = toggle;
            _repository.MarkDirty();
        }

        /// <summary>しゃがみ入力モードを保持する（false=ホールド, true=トグル）。</summary>
        public void SetCrouchToggle(bool toggle)
        {
            if (_repository.Data == null) return;
            _repository.Data.CrouchToggle = toggle;
            _repository.MarkDirty();
        }

        #endregion

        #region Graphics

        /// <summary>画面表示モード（ウィンドウ/フルスクリーン等）を設定する。</summary>
        public void SetDisplayMode(FullScreenMode mode)
        {
            if (_repository.Data == null) return;
            _repository.Data.DisplayMode = mode;
            _repository.MarkDirty();
        }

        /// <summary>解像度（幅・高さ）を設定する。</summary>
        public void SetResolution(int width, int height)
        {
            if (_repository.Data == null) return;
            _repository.Data.ResolutionWidth = width;
            _repository.Data.ResolutionHeight = height;
            _repository.MarkDirty();
        }

        /// <summary>フレームレート上限を設定する。</summary>
        public void SetFrameRateLimit(int fps)
        {
            if (_repository.Data == null) return;
            _repository.Data.FrameRateLimit = fps;
            _repository.MarkDirty();
        }

        /// <summary>フレームレート上限を無効化するか設定する。</summary>
        public void SetUncappedFrameRate(bool uncapped)
        {
            if (_repository.Data == null) return;
            _repository.Data.UncappedFrameRate = uncapped;
            _repository.MarkDirty();
        }

        public void SetShowFrameRate(bool show)
        {
            if (_repository.Data == null) return;
            _repository.Data.ShowFrameRate = show;
            _repository.MarkDirty();
        }

        /// <summary>VSync の有効・無効を設定する。</summary>
        public void SetVSync(bool enabled)
        {
            if (_repository.Data == null) return;
            _repository.Data.VSync = enabled;
            _repository.MarkDirty();
        }

        #endregion

        #region Controls

        /// <summary>
        /// キーリバインドのオーバーライド JSON を保持する（InputActionAsset.SaveBindingOverridesAsJson の出力）。
        /// null は空文字として扱う（オーバーライド無し）。
        /// </summary>
        public void SetInputBindingOverrides(string json)
        {
            if (_repository.Data == null) return;
            _repository.Data.InputBindingOverridesJson = json ?? "";
            _repository.MarkDirty();
        }

        #endregion

        #region Audio

        /// <summary>マスター音量を設定する（1〜10 にクランプ）。</summary>
        public void SetMasterVolume(float value)
        {
            if (_repository.Data == null) return;
            _repository.Data.MasterVolume = Mathf.Clamp(value, 1f, 10f);
            _repository.MarkDirty();
        }

        /// <summary>BGM 音量を設定する（1〜10 にクランプ）。</summary>
        public void SetBgmVolume(float value)
        {
            if (_repository.Data == null) return;
            _repository.Data.BgmVolume = Mathf.Clamp(value, 1f, 10f);
            _repository.MarkDirty();
        }

        /// <summary>ボイス音量を設定する（1〜10 にクランプ）。</summary>
        public void SetVoiceVolume(float value)
        {
            if (_repository.Data == null) return;
            _repository.Data.VoiceVolume = Mathf.Clamp(value, 1f, 10f);
            _repository.MarkDirty();
        }

        /// <summary>SE 音量を設定する（1〜10 にクランプ）。</summary>
        public void SetSeVolume(float value)
        {
            if (_repository.Data == null) return;
            _repository.Data.SeVolume = Mathf.Clamp(value, 1f, 10f);
            _repository.MarkDirty();
        }

        #endregion
    }
}
