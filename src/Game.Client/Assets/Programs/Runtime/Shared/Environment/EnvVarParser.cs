#if !UNITY_SERVER
using System.Collections.Generic;
using System.IO;

namespace Game.Shared.Environment
{
    /// <summary>
    /// docker/game-server/.env を読み取り、キーバリュー辞書を返す。
    /// Unity Editor の SP ローカルモードで使用。
    /// </summary>
    public static class EnvVarParser
    {
        /// <summary>
        /// .env ファイルをパースして Dictionary を返す。
        /// ファイルが存在しない場合は空の Dictionary を返す。
        /// </summary>
        public static Dictionary<string, string> Parse(string filePath)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return result;

            foreach (var line in File.ReadAllLines(filePath))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed[0] == '#') continue;

                var eqIndex = trimmed.IndexOf('=');
                if (eqIndex <= 0) continue;

                var key = trimmed.Substring(0, eqIndex).Trim();
                var value = trimmed.Substring(eqIndex + 1).Trim();

                // 引用符の除去
                if (value.Length >= 2 &&
                    ((value[0] == '"' && value[value.Length - 1] == '"') ||
                     (value[0] == '\'' && value[value.Length - 1] == '\'')))
                {
                    value = value.Substring(1, value.Length - 2);
                }

                result[key] = value;
            }

            return result;
        }

        /// <summary>
        /// パース結果からキーを取得し、存在しなければフォールバック値を返す。
        /// </summary>
        public static string GetValueOrDefault(Dictionary<string, string> envVars, string key, string fallback)
        {
            return envVars.GetValueOrDefault(key, fallback);
        }
    }
}
#endif
