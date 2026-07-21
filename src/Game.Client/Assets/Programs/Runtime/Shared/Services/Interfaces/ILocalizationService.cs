using R3;
using UnityEngine.Localization;

namespace Game.Shared.Services.Interfaces
{
    public interface ILocalizationService : IGameService
    {
        Locale SelectedLocale { get; }

        Observable<Locale> OnLocaleChanged { get; }

        string GetLocalizedString(string tableName, string localizeKey);

        string GetStringByContextActions(string localizeKey);

        string GetStringByInputControls(string deviceLayoutName, string controlPath, string fallback);

        string GetStringByInteractions(string localizeKey);

        string GetStringByMessages(string localizeKey);

        string GetStringByUITexts(string localizeKey);
    }
}
