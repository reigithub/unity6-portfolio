using Game.Shared.Constants;

namespace Game.Shared.Localization
{
    public static class InteractionLocalizer
    {
        public static string Localize(string localizeKey)
        {
            return LocalizationHelper.GetLocalizedString(LocalizationConstants.InteractionTable , localizeKey);
        }
    }
}
