using System;
using System.Collections.Generic;
using System.Linq;
using Game.Library.Shared.MasterData;
using Game.Library.Shared.MasterData.MemoryTables;
using Game.Shared.Services;
using R3;
using UnityEngine;
using VContainer;

namespace Game.MVP.Survivor.Services
{
    /// <summary>
    /// Survivorステージのウェーブ管理
    /// マスターデータ(SurvivorStageWaveMaster/SurvivorStageWaveEnemyMaster)を解釈し、
    /// 敵スポーン情報の提供とウェーブ進行を管理
    /// ライフサイクル: SurvivorStageSceneが所有、ステージ終了で破棄
    /// </summary>
    public class SurvivorStageWaveManager : IDisposable
    {
        [Inject] private readonly IMasterDataService _masterDataService;

        private readonly ReactiveProperty<int> _currentWave = new(0);
        private readonly ReactiveProperty<int> _enemiesThisWave = new(0);
        private readonly ReactiveProperty<int> _enemiesKilled = new(0);
        private readonly ReactiveProperty<bool> _isAllWavesCleared = new(false);

        public ReadOnlyReactiveProperty<int> CurrentWave => _currentWave;
        public ReadOnlyReactiveProperty<int> EnemiesThisWave => _enemiesThisWave;
        public ReadOnlyReactiveProperty<int> EnemiesKilled => _enemiesKilled;
        public ReadOnlyReactiveProperty<bool> IsAllWavesCleared => _isAllWavesCleared;

        /// <summary>全ウェーブ数</summary>
        public int TotalWaves => _waves?.Length ?? 0;

        /// <summary>現在が最終ウェーブかどうか</summary>
        public bool IsLastWave => _currentWaveIndex >= 0 && _currentWaveIndex >= _waves.Length - 1;

        // ステージのウェーブ情報キャッシュ
        private int _stageId;
        private SurvivorStageWaveMaster[] _waves;
        private int _currentWaveIndex;
        private WaveSpawnInfo _currentSpawnInfo;
        private List<WaveEnemySpawnInfo> _currentEnemySpawnList;

        public void Initialize(int stageId)
        {
            _stageId = stageId;
            var memoryDb = _masterDataService.MemoryDatabase;

            // ステージに紐づくウェーブを取得してウェーブ番号順にソート
            _waves = memoryDb.SurvivorStageWaveMasterTable
                .FindByStageId(stageId)
                .OrderBy(w => w.WaveNumber)
                .ToArray();

            _currentWaveIndex = -1;
            _isAllWavesCleared.Value = false;

            Debug.Log($"[SurvivorStageWaveManager] Initialized for stage {stageId}. Total waves: {_waves.Length}");
        }

        public void StartWave()
        {
            _currentWaveIndex++;
            _enemiesKilled.Value = 0;

            if (_currentWaveIndex >= _waves.Length)
            {
                // 全ウェーブクリア
                _isAllWavesCleared.Value = true;
                _currentWaveIndex = _waves.Length - 1;
                Debug.Log($"[SurvivorStageWaveManager] All waves completed!");
                return;
            }

            var wave = _waves[_currentWaveIndex];

            // ウェーブに紐づく敵スポーン情報を取得
            var waveEnemies = _masterDataService.MemoryDatabase.SurvivorStageWaveEnemyMasterTable
                .FindByWaveId(wave.Id)
                .ToArray();

            // スポーン情報を構築（CurrentWave更新前に設定）
            _currentSpawnInfo = new WaveSpawnInfo
            {
                WaveId = wave.Id,
                WaveNumber = wave.WaveNumber,
                EnemyCount = waveEnemies.Sum(e => e.SpawnCount),
                SpawnInterval = waveEnemies.Length > 0 ? waveEnemies[0].SpawnInterval / 1000f : 1f,
                EnemySpeedMultiplier = wave.EnemySpeedMultiplier / 100f,
                EnemyHealthMultiplier = wave.EnemyHealthMultiplier / 100f,
                EnemyDamageMultiplier = wave.EnemyDamageMultiplier / 100f,
                ExperienceMultiplier = wave.ExperienceMultiplier / 100f
            };

            // 敵スポーンリストを構築
            _currentEnemySpawnList = waveEnemies.Select(e => new WaveEnemySpawnInfo
            {
                EnemyId = e.EnemyId,
                SpawnCount = e.SpawnCount,
                SpawnInterval = e.SpawnInterval / 1000f,
                SpawnDelay = e.SpawnDelay / 1000f,
                MinSpawnDistance = e.MinSpawnDistance,
                MaxSpawnDistance = e.MaxSpawnDistance
            }).ToList();

            _enemiesThisWave.Value = _currentSpawnInfo.EnemyCount;

            Debug.Log($"[SurvivorStageWaveManager] Wave {wave.WaveNumber} started. Enemies: {_enemiesThisWave.Value}");

            // 最後にCurrentWaveを更新（サブスクライバーに通知）
            _currentWave.Value = wave.WaveNumber;
        }

        public void OnEnemyKilled()
        {
            _enemiesKilled.Value++;

            // 全ての敵を倒したら次のウェーブへ
            if (_enemiesKilled.Value >= _enemiesThisWave.Value)
            {
                StartWave();
            }
        }

        public WaveSpawnInfo GetSpawnInfo()
        {
            return _currentSpawnInfo ?? new WaveSpawnInfo
            {
                WaveNumber = 1,
                EnemyCount = 5,
                SpawnInterval = 2f,
                EnemySpeedMultiplier = 1f,
                EnemyHealthMultiplier = 1f,
                EnemyDamageMultiplier = 1f,
                ExperienceMultiplier = 1f
            };
        }

        public IReadOnlyList<WaveEnemySpawnInfo> GetEnemySpawnList()
        {
            return _currentEnemySpawnList ?? new List<WaveEnemySpawnInfo>();
        }

        public void Dispose()
        {
            _currentWave.Dispose();
            _enemiesThisWave.Dispose();
            _enemiesKilled.Dispose();
            _isAllWavesCleared.Dispose();
        }
    }

    /// <summary>
    /// ウェーブスポーン情報
    /// </summary>
    public class WaveSpawnInfo
    {
        public int WaveId { get; set; }
        public int WaveNumber { get; set; }
        public int EnemyCount { get; set; }
        public float SpawnInterval { get; set; }
        public float EnemySpeedMultiplier { get; set; }
        public float EnemyHealthMultiplier { get; set; }
        public float EnemyDamageMultiplier { get; set; }
        public float ExperienceMultiplier { get; set; }
    }

    /// <summary>
    /// ウェーブ敵スポーン情報
    /// </summary>
    public class WaveEnemySpawnInfo
    {
        public int EnemyId { get; set; }
        public int SpawnCount { get; set; }
        public float SpawnInterval { get; set; }
        public float SpawnDelay { get; set; }
        public float MinSpawnDistance { get; set; }
        public float MaxSpawnDistance { get; set; }
    }
}
