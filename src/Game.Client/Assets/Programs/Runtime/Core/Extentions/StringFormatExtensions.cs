namespace Game.Core.Extensions
{
    public static class StringFormatExtensions
    {
        public static string FormatToTimer(this int time)
        {
            int minutes = time / 60;
            int seconds = time % 60;
            return $"{minutes:00}:{seconds:00}";
        }

        public static string FormatToTimer(this float time)
        {
            return FormatToTimer((int)time);
        }
    }
}