using UnityEngine;

namespace Game.Core
{
    public enum GameEnv
    {
        Local = 0,
        Develop = 1,
        Staging = 2,
        Review = 3,
        Release = 4,
    }

    [CreateAssetMenu(fileName = "GameConfig", menuName = "Project/GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [SerializeField] private GameEnv env;

        public GameEnv Env => env;
    }
}