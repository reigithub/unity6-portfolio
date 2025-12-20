using UnityEngine;

namespace Game.Core
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Project/GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [SerializeField] private GameEnv env;

        public GameEnv Env => env;
    }
}