using UnityEngine;
using UnityEngine.Localization.Settings;

namespace Game.Horror.SaveData
{
    public static class HorrorOptionHelper
    {
        public static void ApplySaveData(HorrorOptionSaveData d)
        {
            if (d == null) return;
            ApplyLanguage(d.LanguageCode);
            ApplyResolution(d.DisplayMode, d.ResolutionWidth, d.ResolutionHeight);
            ApplyFrameRate(d.VSync, d.UncappedFrameRate, d.FrameRateLimit);
        }

        public static void ApplyLanguage(string code)
        {
            if (string.IsNullOrEmpty(code)) return;
            var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
            if (locale != null) LocalizationSettings.SelectedLocale = locale;
        }

        /// <summary>表示モードと解像度は一体で適用。幅/高さが 0 のときは現在の解像度を使用。</summary>
        public static void ApplyResolution(FullScreenMode mode, int width, int height)
        {
            var w = width > 0 ? width : Screen.currentResolution.width;
            var h = height > 0 ? height : Screen.currentResolution.height;
            Screen.SetResolution(w, h, mode);
        }

        /// <summary>フレームレート上限/上限解除/VSync は相互依存のため一体で適用。</summary>
        public static void ApplyFrameRate(bool vSync, bool uncapped, int limit)
        {
            QualitySettings.vSyncCount = vSync ? 1 : 0;
            Application.targetFrameRate = ResolveTargetFrameRate(vSync, uncapped, limit);
        }

        /// <summary>
        /// VSync 有効 or 上限解除なら -1（VSync 時は targetFrameRate 無効、上限解除は無制限）。
        /// それ以外は上限値。純粋関数（テスト対象）。
        /// </summary>
        public static int ResolveTargetFrameRate(bool vSync, bool uncapped, int limit)
        {
            return (vSync || uncapped) ? -1 : limit;
        }
    }
}
