using Game.Shared.Constants;

namespace Game.Shared.Extensions
{
    public static class StringFormatExtensions
    {
        public static string FormatToTimer(this int time)
        {
            int minutes = time / TimeConstants.SecondsPerMinute;
            int seconds = time % TimeConstants.SecondsPerMinute;
            return $"{minutes:00}:{seconds:00}";
        }

        public static string FormatToTimer(this float time)
        {
            return FormatToTimer((int)time);
        }
    }
}