using System;
using System.Collections.Generic;

namespace Game.Shared.Environment
{
    public static class ClArgsHelper
    {
        public static Dictionary<string, string> Parse()
        {
            var args = new Dictionary<string, string>();
            string[] clArgs = System.Environment.GetCommandLineArgs();
            if (clArgs.Length <= 0) return args;
            for (int i = 0; i < clArgs.Length - 1; i++)
            {
                if (clArgs[i].StartsWith("--")) args[clArgs[i]] = clArgs[i + 1];
            }

            return args;
        }

        public static bool TryGet<T>(Dictionary<string, string> args, string key, out T value, Func<string, T> converter)
        {
            if (!args.TryGetValue(key, out string val))
            {
                value = default;
                return false;
            }

            value = converter.Invoke(val);
            return true;
        }
    }
}
