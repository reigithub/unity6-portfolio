using System;
using Game.Shared.Constants;
using Game.Shared.Enums;
using Game.Shared.Input;
using Game.Shared.Services.Interfaces;
using R3;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Game.Shared.Services
{
    public abstract class LocalizationServiceBase : ILocalizationService
    {
        public Locale SelectedLocale => LocalizationSettings.SelectedLocale;

        public Observable<Locale> OnLocaleChanged
            => Observable.FromEvent<Action<Locale>, Locale>(
                h => h,
                h => LocalizationSettings.SelectedLocaleChanged += h,
                h => LocalizationSettings.SelectedLocaleChanged -= h);

        public string GetLocalizedString(string tableName, string localizeKey)
        {
            var entry = LocalizationSettings.StringDatabase.GetTableEntry(tableName, localizeKey).Entry;
            return entry != null ?　entry.GetLocalizedString() : null;
        }

        public string GetStringByContextActions(string localizeKey)
            => GetLocalizedString(LocalizationConstants.ContextActionsTable, localizeKey);

        /// <summary>
        /// controlPath をキーに InputControls からローカライズ名を引く。
        /// family プレフィックス付きキー → 無印キー → fallback(raw) の順に解決する。
        /// </summary>
        /// <param name="deviceLayoutName">解決済みコントロールのデバイスレイアウト名（family 判定用。null/空可）。</param>
        /// <param name="controlPath"></param>
        /// <param name="fallback"></param>
        public string GetStringByInputControls(string deviceLayoutName, string controlPath, string fallback)
        {
            if (string.IsNullOrEmpty(controlPath)) return fallback;

            var prefix = ResolveDevicePrefix(deviceLayoutName);
            if (prefix.Length > 0)
            {
                var localized = GetLocalizedString(LocalizationConstants.InputControlsTable, prefix + controlPath);
                if (!string.IsNullOrEmpty(localized)) return localized;
            }

            return GetLocalizedString(LocalizationConstants.InputControlsTable, controlPath) ?? fallback;
        }

        /// <summary>デバイスレイアウトを family プレフィックスへ分類する（未知/未接続は空＝無印）。</summary>
        private static string ResolveDevicePrefix(string deviceLayoutName)
        {
            var deviceName = InputSystemHelper.GetInputDeviceType(deviceLayoutName).ToIdentifier();
            if (string.IsNullOrEmpty(deviceName)) return string.Empty;
            return deviceName + "/";
        }

        public string GetStringByInteractions(string localizeKey)
            => GetLocalizedString(LocalizationConstants.InteractionsTable, localizeKey);

        public string GetStringByMessages(string localizeKey)
            => GetLocalizedString(LocalizationConstants.InteractionMessagesTable, localizeKey);

        public string GetStringByPropTexts(string localizeKey)
            => GetLocalizedString(LocalizationConstants.PropTextsTable, localizeKey);

        public string GetStringByUITexts(string localizeKey)
            => GetLocalizedString(LocalizationConstants.UITextsTable, localizeKey);
    }
}

