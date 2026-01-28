using Game.Shared.Constants;

namespace Game.Shared.Extensions
{
    /// <summary>
    /// 文字列フォーマット用の拡張メソッド
    /// </summary>
    public static class StringFormatExtensions
    {
        /// <summary>
        /// 秒数をタイマー形式（MM:SS）の文字列に変換する
        /// </summary>
        /// <param name="time">秒数</param>
        /// <returns>"00:00"形式の文字列</returns>
        public static string FormatToTimer(this int time)
        {
            int minutes = time / TimeConstants.SecondsPerMinute;
            int seconds = time % TimeConstants.SecondsPerMinute;
            return $"{minutes:00}:{seconds:00}";
        }

        /// <summary>
        /// 秒数をタイマー形式（MM:SS）の文字列に変換する
        /// </summary>
        /// <param name="time">秒数（小数点以下は切り捨て）</param>
        /// <returns>"00:00"形式の文字列</returns>
        public static string FormatToTimer(this float time)
        {
            return FormatToTimer((int)time);
        }
    }
}