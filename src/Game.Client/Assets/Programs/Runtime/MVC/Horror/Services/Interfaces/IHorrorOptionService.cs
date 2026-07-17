using Game.Core.Services;
using UnityEngine;

namespace Game.Horror.Services.Interfaces
{
    /// <summary>
    /// Horror オプション設定のビジネスロジックを扱うドメインサービスのインターフェース。
    /// 値のクランプ・正規化・Dirty 化を担う。永続化（読み込み・保存）は <see cref="IHorrorOptionSaveRepository"/> の面。
    /// </summary>
    public interface IHorrorOptionService : IGameService
    {
        /// <summary>表示言語コードを設定する。</summary>
        void SetLanguageCode(string code);

        /// <summary>カメラの水平軸反転設定を保持する。</summary>
        void SetCameraControlHorizontal(bool invert);

        /// <summary>カメラの垂直軸反転設定を保持する。</summary>
        void SetCameraControlVertical(bool invert);

        /// <summary>カメラの水平感度を設定する。</summary>
        void SetCameraSensitivityHorizontal(float value);

        /// <summary>カメラの垂直感度を設定する。</summary>
        void SetCameraSensitivityVertical(float value);

        /// <summary>カメラの加速度を設定する。</summary>
        void SetCameraAcceleration(float value);

        /// <summary>カメラの揺れ強度を設定する。</summary>
        void SetCameraShake(float value);

        /// <summary>カメラの視野角（FOV）を設定する。</summary>
        void SetCameraFov(float fov);

        /// <summary>画面表示モード（ウィンドウ/フルスクリーン等）を設定する。</summary>
        void SetDisplayMode(FullScreenMode mode);

        /// <summary>解像度（幅・高さ）を設定する。</summary>
        void SetResolution(int width, int height);

        /// <summary>フレームレート上限を設定する。</summary>
        void SetFrameRateLimit(int fps);

        /// <summary>フレームレート上限を無効化するか設定する。</summary>
        void SetUncappedFrameRate(bool uncapped);

        /// <summary>フレームレートを表示するか</summary>
        void SetShowFrameRate(bool show);

        /// <summary>VSync の有効・無効を設定する。</summary>
        void SetVSync(bool enabled);

        /// <summary>走り入力モードを保持する（false=ホールド, true=トグル）。</summary>
        void SetSprintToggle(bool toggle);

        /// <summary>しゃがみ入力モードを保持する（false=ホールド, true=トグル）。</summary>
        void SetCrouchToggle(bool toggle);

        /// <summary>
        /// キーリバインドのオーバーライド JSON を保持する（InputActionAsset.SaveBindingOverridesAsJson の出力）。
        /// null は空文字として扱う（オーバーライド無し）。
        /// </summary>
        void SetInputBindingOverrides(string json);

        /// <summary>マスター音量を設定する（1〜10 にクランプ）。</summary>
        void SetMasterVolume(float value);

        /// <summary>BGM 音量を設定する（1〜10 にクランプ）。</summary>
        void SetBgmVolume(float value);

        /// <summary>ボイス音量を設定する（1〜10 にクランプ）。</summary>
        void SetVoiceVolume(float value);

        /// <summary>SE 音量を設定する（1〜10 にクランプ）。</summary>
        void SetSeVolume(float value);
    }
}
