using UnityEngine.Localization.Settings;

namespace Game.Shared.Localization
{
    /// <summary>
    /// 入力コントロールの表示名をローカライズする。
    /// controlPath（"space", "buttonSouth", "dpad/up" 等、デバイス接頭辞なし）をキーに
    /// StringTable "InputControls" を引き、未登録キーは Unity 既定の英語表示へフォールバックする。
    /// </summary>
    public static class InputControlLocalizer
    {
        private const string TableName = "InputControls";

        /// <summary>controlPath をキーに InputControls からローカライズ名を引く。未登録は fallback。</summary>
        public static string Localize(string controlPath, string fallback)
        {
            if (string.IsNullOrEmpty(controlPath)) return fallback;
            var entry = LocalizationSettings.StringDatabase.GetTableEntry(TableName, controlPath).Entry;
            return entry != null ? entry.GetLocalizedString() : fallback;
        }
    }
}
