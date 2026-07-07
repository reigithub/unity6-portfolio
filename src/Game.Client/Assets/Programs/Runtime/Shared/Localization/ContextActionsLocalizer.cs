using Game.Shared.Constants;

namespace Game.Shared.Localization
{
    public static class ContextActionsLocalizer
    {
        public static string Localize(string localizeKey)
            => LocalizationHelper.GetLocalizedString(LocalizationConstants.ContextActionsTable , localizeKey);
    }
}
