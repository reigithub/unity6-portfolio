using Game.ScoreTimeAttack.Enemy;
using Game.ScoreTimeAttack.Item;
using Game.ScoreTimeAttack.Player;
using Game.MVC.Core.Scenes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.ScoreTimeAttack.Scenes
{
    public static class ScoreTimeAttackStageSceneHelper
    {
        public static ScoreTimeAttackPlayerStart GetPlayerStart(Scene scene)
        {
            return GameSceneHelper.GetComponentInChildren<ScoreTimeAttackPlayerStart>(scene);
        }

        public static ScoreTimeAttackEnemyStart[] GetEnemyStarts(Scene scene)
        {
            return GameSceneHelper.GetComponentsInChildren<ScoreTimeAttackEnemyStart>(scene);
        }

        public static ScoreTimeAttackStageItemStart[] GetStageItemStarts(Scene scene)
        {
            return GameSceneHelper.GetComponentsInChildren<ScoreTimeAttackStageItemStart>(scene);
        }

        public static Skybox GetSkybox(Scene scene)
        {
            return GameSceneHelper.GetComponentInChildren<Skybox>(scene);
        }
    }
}