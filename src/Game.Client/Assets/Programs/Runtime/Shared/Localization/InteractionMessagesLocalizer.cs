using Game.Shared.Constants;

namespace Game.Shared.Localization
{
    public static class InteractionMessagesLocalizer
    {
        public static string Localize(string localizeKey)
            => LocalizationHelper.GetLocalizedString(LocalizationConstants.InteractionMessagesTable , localizeKey);
    }
}
