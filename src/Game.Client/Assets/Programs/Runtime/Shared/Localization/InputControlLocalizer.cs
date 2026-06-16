using UnityEngine.InputSystem;
using UnityEngine.Localization.Settings;

namespace Game.Shared.Localization
{
    /// <summary>
    /// 入力コントロールの表示名をローカライズする。
    /// controlPath（"space", "buttonSouth", "dpad/up" 等、デバイス接頭辞なし）をキーに
    /// StringTable "InputControls" を引き、未登録キーは Unity 既定の英語表示へフォールバックする。
    /// ゲームパッドは接続デバイスの family（xbox/ps/switch）でプレフィックス付きキーを優先的に引く。
    /// </summary>
    public static class InputControlLocalizer
    {
        private const string TableName = "InputControls";

        /// <summary>
        /// controlPath をキーに InputControls からローカライズ名を引く。
        /// family プレフィックス付きキー → 無印キー → fallback(raw) の順に解決する。
        /// </summary>
        /// <param name="deviceLayoutName">解決済みコントロールのデバイスレイアウト名（family 判定用。null/空可）。</param>
        /// <param name="controlPath"></param>
        /// <param name="fallback"></param>
        public static string Localize(string deviceLayoutName, string controlPath, string fallback)
        {
            if (string.IsNullOrEmpty(controlPath)) return fallback;

            var prefix = ResolveFamilyPrefix(deviceLayoutName);
            if (prefix.Length > 0)
            {
                var familyEntry = LocalizationSettings.StringDatabase.GetTableEntry(TableName, prefix + controlPath).Entry;
                if (familyEntry != null) return familyEntry.GetLocalizedString();
            }

            var entry = LocalizationSettings.StringDatabase.GetTableEntry(TableName, controlPath).Entry;
            return entry != null ? entry.GetLocalizedString() : fallback;
        }

        /// <summary>デバイスレイアウトを family プレフィックスへ分類する（未知/未接続は空＝無印）。</summary>
        private static string ResolveFamilyPrefix(string deviceLayoutName)
        {
            if (string.IsNullOrEmpty(deviceLayoutName)) return string.Empty;
            if (InputSystem.IsFirstLayoutBasedOnSecond(deviceLayoutName, "DualShockGamepad")) return "ps/";
            if (InputSystem.IsFirstLayoutBasedOnSecond(deviceLayoutName, "SwitchProControllerHID")) return "switch/";
            if (InputSystem.IsFirstLayoutBasedOnSecond(deviceLayoutName, "XInputController")) return "xbox/";
            return string.Empty;
        }
    }
}
