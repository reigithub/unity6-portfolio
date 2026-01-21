using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// Survivorステージシーンのヘルパー
    /// ステージ環境シーン内のコンポーネントを取得するユーティリティ
    /// </summary>
    public static class SurvivorStageSceneHelper
    {
        /// <summary>
        /// シーン内のプレイヤースタート地点を取得
        /// </summary>
        public static SurvivorPlayerStart GetPlayerStart(IObjectResolver resolver, Scene scene)
        {
            var playerStart = GameSceneHelper.GetComponentInChildren<SurvivorPlayerStart>(scene);
            if (playerStart != null)
            {
                resolver.Inject(playerStart);
            }

            return playerStart;
        }

        /// <summary>
        /// シーン内のスカイボックスを取得
        /// </summary>
        public static Skybox GetSkybox(Scene scene)
        {
            return GameSceneHelper.GetComponentInChildren<Skybox>(scene);
        }
    }
}