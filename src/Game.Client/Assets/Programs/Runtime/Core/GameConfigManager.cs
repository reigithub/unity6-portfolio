using UnityEngine;

namespace Game.Core
{
    public static class GameConfigManager
    {
        private const string AssetPath = "GameConfigs";
        private const string PlayerPrefsKey = "GameConfig";

        public static GameConfig Load()
        {
            var env = GetEnv();
            var assetPath = AssetPath + "/GameConfig." + env;
            return Resources.Load<GameConfig>(assetPath);
        }

        public static GameConfig[] LoadAll()
        {
            return Resources.LoadAll<GameConfig>(AssetPath);
        }

        public static GameEnv GetEnv()
        {
#if RELEASE
            return GameEnvironment.Release;
#else
            return (GameEnv)PlayerPrefs.GetInt(PlayerPrefsKey, 0);
#endif
        }

        public static void SetEnv(GameEnv env)
        {
            PlayerPrefs.SetInt(PlayerPrefsKey, (int)env);
            PlayerPrefs.Save();
        }
    }
}