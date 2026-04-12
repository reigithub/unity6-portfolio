using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Game.Shared.Environment
{
    /// <summary>
    /// docker/game-server/.env を読み取り、キーバリュー辞書を返す。
    /// </summary>
    public static class EnvVarHelper
    {
        private const string DefaultEnvRelativePath = "docker/game-server/.env";
        private const int MaxAncestorSearchDepth = 8;

        /// <summary>
        /// docker/game-server/.env を <see cref="Application.dataPath"/> から親方向に探索する。
        /// 見つからなければ null を返す。
        /// Unity メインスレッドから呼ぶこと。
        /// </summary>
        public static string FindDefaultEnvFile()
        {
            var dir = Application.dataPath;
            for (var i = 0; i < MaxAncestorSearchDepth; i++)
            {
                var candidate = Path.Combine(dir, DefaultEnvRelativePath);
                if (File.Exists(candidate)) return candidate;

                var parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }
            return null;
        }

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
        public static string GetValueOrDefault(Dictionary<string, string> envVars, string key, string defaultValue)
            => envVars.GetValueOrDefault(key, defaultValue);

        public static bool TryGetValue(Dictionary<string, string> envVars, string key, out string value)
        {
            if (envVars.TryGetValue(key, out value))
                return !string.IsNullOrEmpty(value);

            return false;
        }

        public static bool TryGetValueOrDefault(Dictionary<string, string> envVars, string key, string defaultValue, out string value)
        {
            if (envVars.TryGetValue(key, out value))
                return !string.IsNullOrEmpty(value);

            value = defaultValue;
            return false;
        }

        public static bool TryGet(string key, out string value)
        {
            value = Get(key);
            return !string.IsNullOrEmpty(value);
        }

        public static bool TryGet<T>(string key, out T value, Func<string, T> converter)
        {
            if (!TryGet(key, out string val))
            {
                value = default;
                return false;
            }

            // パースは成功する前提
            value = converter.Invoke(val);
            return true;
        }

        public static string Get(string key)
        {
            return System.Environment.GetEnvironmentVariable(key);
        }

        public static void Set(string key, string value)
        {
            if (!string.IsNullOrEmpty(value)) System.Environment.SetEnvironmentVariable(key, value);
        }

        public static void Set(string key, Func<string> getter)
        {
            var envVar = System.Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrEmpty(envVar))
            {
                var value = getter.Invoke();
                if (!string.IsNullOrEmpty(value))
                {
                    System.Environment.SetEnvironmentVariable(key, value);
                    Debug.Log($"System.Environment Set => Key: {key} Value: {value}");
                }
            }
        }
    }
}
