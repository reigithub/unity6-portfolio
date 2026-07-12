using Game.Shared.Constants;

namespace Game.Shared.Localization
{
    public static class InteractionsLocalizer
    {
        public static string Localize(string localizeKey)
            => LocalizationHelper.GetLocalizedString(LocalizationConstants.InteractionsTable , localizeKey);
    }
}
