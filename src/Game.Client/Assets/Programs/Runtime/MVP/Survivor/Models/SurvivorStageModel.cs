using System;
using Game.Library.Shared.MasterData;
using Game.Library.Shared.MasterData.MemoryTables;
using Game.Shared.Services;
using R3;
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
        public bool IsDead => CurrentHp.Value <= 0;

        /// <summary>制限時間（秒）。0以下は無制限</summary>
        public float TimeLimit => _stageMaster?.TimeLimit ?? 0;

        /// <summary>制限時間に到達したかどうか</summary>
        public bool IsTimeUp => TimeLimit > 0 && GameTime.Value >= TimeLimit;

        public void Initialize(int playerId, int stageId)
        {
            var memoryDb = _masterDataService.MemoryDatabase;

            if (!memoryDb.SurvivorPlayerMasterTable.TryFindById(playerId, out _playerMaster))
            {
                throw new InvalidOperationException($"Player master not found: {playerId}");
            }

            if (!memoryDb.SurvivorStageMasterTable.TryFindById(stageId, out _stageMaster))
            {
                throw new InvalidOperationException($"Stage master not found: {stageId}");
            }

            MaxHp.Value = _playerMaster.MaxHp;
            CurrentHp.Value = _playerMaster.MaxHp;

            UpdateLevelStats();
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
            if (_masterDataService.MemoryDatabase.SurvivorLevelUpMasterTable.TryFindByLevel(Level.Value, out var levelMaster))
            {
                ExperienceToNextLevel.Value = levelMaster.RequiredExperience;
                DamageBonus.Value = levelMaster.DamageBonus;
                WeaponChoiceCount.Value = levelMaster.WeaponChoiceCount;

                if (levelMaster.HpBonus > 0)
                {
                    MaxHp.Value += levelMaster.HpBonus;
                    CurrentHp.Value = Math.Min(CurrentHp.Value + levelMaster.HpBonus, MaxHp.Value);
                }
            }
            else
            {
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

        public void AddKill()
        {
            TotalKills.Value++;
            Score.Value += 100;
        }

        public float GetDamageMultiplier()
        {
            return 1f + (DamageBonus.Value / 100f);
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