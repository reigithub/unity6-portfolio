using MemoryPack;
using UnityEngine;

namespace Game.Horror.SaveData
{
    /// <summary>
    /// Horror オプション画面（一般/表示/グラフィック/音量）の設定セーブデータ。
    /// MemoryPack でバイナリ永続化する。enum は underlying value、解像度は Width/Height に分解して保持。
    /// </summary>
    [MemoryPackable]
    public partial class HorrorOptionSaveData
    {
        /// <summary>セーブデータバージョン（マイグレーション用）</summary>
        public int Version { get; set; } = 1;

        #region Gameplay

        /// <summary>選択中のロケールコード（例: "ja", "en"）</summary>
        public string LanguageCode { get; set; } = "ja";

        /// <summary>カメラ左右反転</summary>
        public bool CameraControlHorizontal { get; set; }

        /// <summary>カメラ上下反転</summary>
        public bool CameraControlVertical { get; set; }

        /// <summary>カメラ水平感度</summary>
        public float CameraSensitivityHorizontal { get; set; } = 1f;

        /// <summary>カメラ垂直感度</summary>
        public float CameraSensitivityVertical { get; set; } = 1f;

        /// <summary>カメラ加速</summary>
        public float CameraAcceleration { get; set; } = 15f;

        /// <summary>カメラシェイク強度</summary>
        public float CameraShake { get; set; } = 1f;

        /// <summary>カメラ FOV</summary>
        public float CameraFov { get; set; } = 60f;

        /// <summary>走り入力モード（false=ホールド, true=トグル）</summary>
        public bool SprintToggle { get; set; }

        /// <summary>しゃがみ入力モード（false=ホールド, true=トグル）</summary>
        public bool CrouchToggle { get; set; } = true;

        #endregion

        #region Graphics

        /// <summary>表示モード（フルスクリーン/ウィンドウ等）</summary>
        public FullScreenMode DisplayMode { get; set; } = FullScreenMode.FullScreenWindow;

        /// <summary>解像度 幅（0 = 現在の解像度を使用）</summary>
        public int ResolutionWidth { get; set; }

        /// <summary>解像度 高さ（0 = 現在の解像度を使用）</summary>
        public int ResolutionHeight { get; set; }

        /// <summary>フレームレート上限（fps）</summary>
        public int FrameRateLimit { get; set; } = 60;

        /// <summary>フレームレート上限を解除する</summary>
        public bool UncappedFrameRate { get; set; }

        /// <summary>垂直同期</summary>
        public bool VSync { get; set; }

        #endregion

        #region Controls

        /// <summary>
        /// キーリバインドのオーバーライド（InputActionAsset.SaveBindingOverridesAsJson の出力）。
        /// 空文字はオーバーライド無し（既定バインド）を表す。
        /// </summary>
        public string InputBindingOverridesJson { get; set; } = "";

        #endregion

        #region Audio

        // 値は SliderValueSelector の実値（float）。range/既定は prefab のスライダー設定が確定した時点で揃える。

        /// <summary>マスターボリューム</summary>
        public float MasterVolume { get; set; } = 1f;

        /// <summary>BGM ボリューム</summary>
        public float BgmVolume { get; set; } = 1f;

        /// <summary>ボイスボリューム</summary>
        public float VoiceVolume { get; set; } = 1f;

        /// <summary>SE ボリューム</summary>
        public float SeVolume { get; set; } = 1f;

        #endregion
    }
}
