#if UNITY_SERVER
using Game.MVP.Survivor.Scenes.Models;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// サーバー用ステージビュー（全メソッドno-op）
    /// サーバーではHUD更新が不要なため、NullObjectパターンで安全にスキップ
    /// </summary>
    public class NullSurvivorStageSceneView : ISurvivorStageSceneView
    {
        public void Initialize(SurvivorStageModel model, int totalWaves) { }
        public void InitializeWeaponDisplay() { }
        public void UpdateHp(int current, int max) { }
        public void UpdateStamina(int current, int max) { }
        public void UpdateExperience(int current, int max) { }
        public void UpdateLevel(int level) { }
        public void UpdateTime(float time) { }
        public void UpdateKills(int kills) { }
        public void UpdateWave(int wave, int totalWaves) { }
        public void UpdateEnemies(int killed, int total) { }
        public void ShowWaveBanner(int wave, int totalWaves, int enemyCount) { }
        public void ShowGameOver() { }
        public void ShowVictory() { }
        public void SetHudVisible(bool visible, bool immediate = false) { }
    }
}
#endif
