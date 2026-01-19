using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Shared
{
    [CreateAssetMenu(fileName = "GameEnvironmentSettings", menuName = "Project/Game Environment Settings")]
    public class GameEnvironmentSettings : ScriptableObject
    {
        [Header("Environment")]
        [SerializeField] private GameEnvironment _environment = GameEnvironment.Develop;

        [Header("API Endpoints")]
        [SerializeField] private GameEnvironmentConfig[] _configs;

        public GameEnvironment Environment => _environment;
        public GameEnvironmentConfig CurrentConfig => GetConfig(_environment);
        public GameEnvironmentConfig[] AllConfigs => _configs;

        private GameEnvironmentConfig GetConfig(GameEnvironment environment = GameEnvironment.Develop)
        {
            foreach (var config in _configs)
            {
                if (config.Environment == environment)
                    return config;
            }

            return null;
        }

        public void SetConfig(GameEnvironment environment = GameEnvironment.Develop)
        {
#if UNITY_EDITOR
            var so = new SerializedObject(Instance);
            so.FindProperty("_environment").intValue = (int)environment;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssetIfDirty(this);
#endif
        }

        private static GameEnvironmentSettings _instance;

        public static GameEnvironmentSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<GameEnvironmentSettings>("GameEnvironmentSettings");
                }

                return _instance;
            }
        }
    }
}
