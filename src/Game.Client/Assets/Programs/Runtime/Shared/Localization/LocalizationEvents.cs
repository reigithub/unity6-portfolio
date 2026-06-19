using System;
using R3;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Game.Shared.Localization
{
    public static class LocalizationEvents
    {
        public static Observable<Locale> OnLocaleChanged
            => Observable.FromEvent<Action<Locale>, Locale>(
                h => h,
                h => LocalizationSettings.SelectedLocaleChanged += h,
                h => LocalizationSettings.SelectedLocaleChanged -= h);

    }
}
