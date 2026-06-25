using UnityEngine.Localization.Settings;

namespace Game.Shared.Localization
{
    public static class LocalizationHelper
    {
        public static string GetLocalizedString(string tableName, string localizeKey)
        {
            var entry = LocalizationSettings.StringDatabase.GetTableEntry(tableName, localizeKey).Entry;
            return entry != null ?　entry.GetLocalizedString() : null;
        }
    }
}
