using System;
using Game.Library.Shared.MasterData;
using Game.Library.Shared.MasterData.MemoryTables;
using Game.MVP.Survivor.Item;
using Game.Shared.Extensions;
using Game.Shared.Services;
using R3;
using UnityEngine;
using VContainer;

namespace Game.MVP.Survivor.Models
{
    /// <summary>
    /// Survivorステージモデル
    /// 1ステージ分のプレイヤー状態を管理
    /// ライフサイクル: SurvivorStageSceneが所有、ステージ終了で破棄
    /// </summary>
    public class SurvivorStageModel : IDisposable
    {
        [Inject] private readonly IMasterDataService _masterDataService;

        private SurvivorPlayerMaster _playerMaster;
        private SurvivorStageMaster _stageMaster;
        private SurvivorPlayerLevelMaster _currentLevelMaster;
        private int _playerId;

        // プレイヤー状態
        public ReactiveProperty<int> CurrentHp { get; } = new(100);
        public ReactiveProperty<int> MaxHp { get; } = new(100);
        public ReactiveProperty<int> Level { get; } = new(1);
        public ReactiveProperty<int> Experience { get; } = new(0);
        public ReactiveProperty<int> ExperienceToNextLevel { get; } = new(10);

        // ボーナス
        public ReactiveProperty<int> DamageBonus { get; } = new(0);
        public ReactiveProperty<int> WeaponChoiceCount { get; } = new(3);

        // スコア
        public ReactiveProperty<int> TotalKills { get; } = new(0);
        public ReactiveProperty<int> Score { get; } = new(0);

        // ゲーム進行
        public ReactiveProperty<float> GameTime { get; } = new(0f);
        public ReactiveProperty<int> CurrentWave { get; } = new(1);

        public SurvivorPlayerMaster PlayerMaster => _playerMaster;
        public SurvivorStageMaster StageMaster => _stageMaster;
        public SurvivorPlayerLevelMaster CurrentLevelMaster => _currentLevelMaster;
        public bool IsDead => CurrentHp.Value <= 0;

        /// <summary>制限時間（秒）。0以下は無制限</summary>
        public float TimeLimit => _stageMaster?.TimeLimit ?? 0;

        /// <summary>制限時間に到達したかどうか</summary>
        public bool IsTimeUp => TimeLimit > 0 && GameTime.Value >= TimeLimit;

        public void Initialize(int playerId, int stageId)
        {
            var memoryDb = _masterDataService.MemoryDatabase;
            _playerId = playerId;

            if (!memoryDb.SurvivorPlayerMasterTable.TryFindById(playerId, out _playerMaster))
            {
                throw new InvalidOperationException($"Player master not found: {playerId}");
            }

            if (!memoryDb.SurvivorStageMasterTable.TryFindById(stageId, out _stageMaster))
            {
                throw new InvalidOperationException($"Stage master not found: {stageId}");
            }

            // レベル1のステータスを取得
            UpdateLevelStats();
            CurrentHp.Value = MaxHp.Value;
        }

        public void AddExperience(int amount)
        {
            Experience.Value += amount;

            while (Experience.Value >= ExperienceToNextLevel.Value)
            {
                Experience.Value -= ExperienceToNextLevel.Value;
                Level.Value++;
                UpdateLevelStats();
            }
        }

        private void UpdateLevelStats()
        {
            var memoryDb = _masterDataService.MemoryDatabase;

            if (memoryDb.SurvivorPlayerLevelMasterTable.TryFindByPlayerIdAndLevel((_playerId, Level.Value), out var levelMaster))
            {
                var previousMaxHp = _currentLevelMaster?.MaxHp ?? 0;
                _currentLevelMaster = levelMaster;

                // ステータス更新
                MaxHp.Value = levelMaster.MaxHp;
                ExperienceToNextLevel.Value = levelMaster.RequiredExp;
                DamageBonus.Value = levelMaster.DamageBonus;
                WeaponChoiceCount.Value = levelMaster.WeaponChoiceCount;

                // レベルアップ時のHP増加（差分を回復）
                if (previousMaxHp > 0 && levelMaster.MaxHp > previousMaxHp)
                {
                    var hpIncrease = levelMaster.MaxHp - previousMaxHp;
                    CurrentHp.Value = Math.Min(CurrentHp.Value + hpIncrease, MaxHp.Value);
                }
            }
            else
            {
                // フォールバック（マスターデータが見つからない場合）
                ExperienceToNextLevel.Value = 10 + (Level.Value * 5);
            }
        }

        public void TakeDamage(int damage)
        {
            CurrentHp.Value = Math.Max(0, CurrentHp.Value - damage);
        }

        public void Heal(int amount)
        {
            CurrentHp.Value = Math.Min(MaxHp.Value, CurrentHp.Value + amount);
        }

        /// <summary>
        /// アイテム収集処理
        /// ItemTypeに応じて効果を適用
        /// </summary>
        public void CollectItem(SurvivorItem item)
        {
            switch (item.ItemType)
            {
                case SurvivorItemType.Experience:
                    AddExperience(item.EffectValue);
                    break;

                case SurvivorItemType.Recovery:
                    Heal(item.EffectValue);
                    break;

                default:
                    // throw new NotImplementedException($"ItemType {item.ItemType} is not implemented.");
                    Debug.LogWarning($"ItemType {item.ItemType} is not implemented.");
                    break;
            }
        }

        public void AddKill()
        {
            TotalKills.Value++;
            // スコアはWaveクリア時の残り時間で計算するため、ここでは加算しない
        }

        /// <summary>
        /// Waveクリア時のスコアを加算
        /// 残り時間とHP%が多いほど高スコア
        /// </summary>
        /// <param name="waveNumber">クリアしたWave番号</param>
        /// <param name="remainingTime">残り時間（秒）</param>
        /// <param name="scoreMultiplier">スコア倍率（マスターデータから取得）</param>
        /// <param name="currentHp">現在HP</param>
        /// <param name="maxHp">最大HP</param>
        public void AddWaveClearScore(int waveNumber, float remainingTime, int scoreMultiplier, int currentHp, int maxHp)
        {
            if (remainingTime <= 0) return;

            // HP% (0.0 ~ 1.0)
            var hpRatio = maxHp > 0 ? (float)currentHp / maxHp : 1f;

            // スコア = 残り時間 × ScoreMultiplier × HP%
            var waveScore = (int)(remainingTime * scoreMultiplier * hpRatio);
            Score.Value += waveScore;

            UnityEngine.Debug.Log($"[SurvivorStageModel] Wave {waveNumber} clear! +{waveScore} (Remaining: {remainingTime:F1}s, Multiplier: {scoreMultiplier}, HP: {hpRatio:P0})");
        }

        public float GetDamageMultiplier()
        {
            return 1f + DamageBonus.Value.ToRate();
        }

        public int GetStartingWeaponId()
        {
            return _playerMaster?.StartingWeaponId ?? 1;
        }

        public void Dispose()
        {
            CurrentHp.Dispose();
            MaxHp.Dispose();
            Level.Dispose();
            Experience.Dispose();
            ExperienceToNextLevel.Dispose();
            DamageBonus.Dispose();
            WeaponChoiceCount.Dispose();
            TotalKills.Dispose();
            Score.Dispose();
            GameTime.Dispose();
            CurrentWave.Dispose();
        }
    }
}