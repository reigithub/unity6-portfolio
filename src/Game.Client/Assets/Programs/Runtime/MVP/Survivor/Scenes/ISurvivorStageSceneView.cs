using Game.MVP.Survivor.Scenes.Models;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// ステージシーンのHUD表示インターフェース
    /// サーバーではNullStageViewを使用してno-op化
    /// </summary>
    public interface ISurvivorStageSceneView
    {
        void Initialize(SurvivorStageModel model, int totalWaves);
        void InitializeWeaponDisplay();
        void UpdateHp(int current, int max);
        void UpdateStamina(int current, int max);
        void UpdateExperience(int current, int max);
        void UpdateLevel(int level);
        void UpdateTime(float time);
        void UpdateKills(int kills);
        void UpdateWave(int wave, int totalWaves);
        void UpdateEnemies(int killed, int total);
        void ShowWaveBanner(int wave, int totalWaves, int enemyCount);
        void ShowGameOver();
        void ShowVictory();
        void SetHudVisible(bool visible, bool immediate = false);
    }
}
