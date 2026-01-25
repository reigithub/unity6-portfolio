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
        private readonly ReactiveProperty<int> _targetKillsThisWave = new(0);
        private readonly ReactiveProperty<int> _enemiesKilled = new(0);
        private readonly ReactiveProperty<int> _requiredBossKills = new(0);
        private readonly ReactiveProperty<int> _bossKills = new(0);
        private readonly ReactiveProperty<bool> _isAllWavesCleared = new(false);

        // Waveクリアイベント（クリアしたWave番号を通知）
        private readonly Subject<int> _onWaveCleared = new();
        public Observable<int> OnWaveCleared => _onWaveCleared;

        // キルカウントイベント（目標数に達していない場合のみ発火）
        private readonly Subject<Unit> _onKillCounted = new();
        public Observable<Unit> OnKillCounted => _onKillCounted;

        public ReadOnlyReactiveProperty<int> CurrentWave => _currentWave;
        /// <summary>このWaveの総スポーン数</summary>
        public ReadOnlyReactiveProperty<int> EnemiesThisWave => _enemiesThisWave;
        /// <summary>このWaveのクリア目標数</summary>
        public ReadOnlyReactiveProperty<int> TargetKillsThisWave => _targetKillsThisWave;
        public ReadOnlyReactiveProperty<int> EnemiesKilled => _enemiesKilled;
        /// <summary>このWaveで必要なボス撃破数</summary>
        public ReadOnlyReactiveProperty<int> RequiredBossKills => _requiredBossKills;
        /// <summary>このWaveで撃破したボス数</summary>
        public ReadOnlyReactiveProperty<int> BossKills => _bossKills;
        public ReadOnlyReactiveProperty<bool> IsAllWavesCleared => _isAllWavesCleared;

        /// <summary>全ウェーブ数</summary>
        public int TotalWaves => _waves?.Length ?? 0;

        /// <summary>全Waveの合計目標キル数</summary>
        public int TotalTargetKills
        {
            get
            {
                if (_waves == null || _waves.Length == 0) return 0;
                return _waves.Sum(w => w.TargetKillCount > 0 ? w.TargetKillCount : 0);
            }
        }

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
            _bossKills.Value = 0;

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

            // スポーン総数を計算
            var totalSpawnCount = waveEnemies.Sum(e => e.SpawnCount);

            // 目標キル数を決定（TargetKillCountが0以下の場合はスポーン総数を使用）
            var targetKillCount = wave.TargetKillCount > 0 ? wave.TargetKillCount : totalSpawnCount;

            // 必要ボス撃破数
            var requiredBossKills = wave.RequiredBossKills;

            // スポーン情報を構築（CurrentWave更新前に設定）
            _currentSpawnInfo = new WaveSpawnInfo
            {
                WaveId = wave.Id,
                WaveNumber = wave.WaveNumber,
                EnemyCount = totalSpawnCount,
                TargetKillCount = targetKillCount,
                RequiredBossKills = requiredBossKills,
                SpawnInterval = waveEnemies.Length > 0 ? waveEnemies[0].SpawnInterval / 1000f : 1f,
                EnemySpeedMultiplier = wave.EnemySpeedMultiplier / 100f,
                EnemyHealthMultiplier = wave.EnemyHealthMultiplier / 100f,
                EnemyDamageMultiplier = wave.EnemyDamageMultiplier / 100f,
                ExperienceMultiplier = wave.ExperienceMultiplier / 100f,
                ScoreMultiplier = wave.ScoreMultiplier > 0 ? wave.ScoreMultiplier : 100
            };

            // 敵スポーンリストを構築
            _currentEnemySpawnList = waveEnemies.Select(e => new WaveEnemySpawnInfo
            {
                EnemyId = e.EnemyId,
                SpawnCount = e.SpawnCount,
                SpawnInterval = e.SpawnInterval / 1000f,
                SpawnDelay = e.SpawnDelay / 1000f,
                MinSpawnDistance = e.MinSpawnDistance,
                MaxSpawnDistance = e.MaxSpawnDistance,
                ItemDropGroupId = e.ItemDropGroupId,
                ExpDropGroupId = e.ExpDropGroupId
            }).ToList();

            _enemiesThisWave.Value = totalSpawnCount;
            _targetKillsThisWave.Value = targetKillCount;
            _requiredBossKills.Value = requiredBossKills;

            Debug.Log($"[SurvivorStageWaveManager] Wave {wave.WaveNumber} started. Spawn: {totalSpawnCount}, Target: {targetKillCount}, RequiredBoss: {requiredBossKills}");

            // 最後にCurrentWaveを更新（サブスクライバーに通知）
            _currentWave.Value = wave.WaveNumber;
        }

        public void OnEnemyKilled(bool isBoss = false)
        {
            // 目標数を超える加算をしない
            if (_enemiesKilled.Value < _targetKillsThisWave.Value)
            {
                _enemiesKilled.Value++;
                _onKillCounted.OnNext(Unit.Default);
            }

            // ボス撃破は別カウント（目標数とは独立）
            if (isBoss)
            {
                _bossKills.Value++;
                Debug.Log($"[SurvivorStageWaveManager] Boss killed! ({_bossKills.Value}/{_requiredBossKills.Value})");
            }

            // クリア条件: 目標数達成 AND ボス必要数達成
            var targetKillsReached = _enemiesKilled.Value >= _targetKillsThisWave.Value;
            var bossKillsReached = _bossKills.Value >= _requiredBossKills.Value;

            if (targetKillsReached && bossKillsReached)
            {
                // Waveクリアイベントを発火（現在のWave番号を通知）
                var clearedWave = _currentWave.Value;
                _onWaveCleared.OnNext(clearedWave);

                StartWave();
            }
        }

        public WaveSpawnInfo GetSpawnInfo()
        {
            return _currentSpawnInfo ?? new WaveSpawnInfo
            {
                WaveNumber = 1,
                EnemyCount = 5,
                TargetKillCount = 5,
                RequiredBossKills = 0,
                SpawnInterval = 2f,
                EnemySpeedMultiplier = 1f,
                EnemyHealthMultiplier = 1f,
                EnemyDamageMultiplier = 1f,
                ExperienceMultiplier = 1f,
                ScoreMultiplier = 100
            };
        }

        public IReadOnlyList<WaveEnemySpawnInfo> GetEnemySpawnList()
        {
            return (IReadOnlyList<WaveEnemySpawnInfo>)_currentEnemySpawnList ?? Array.Empty<WaveEnemySpawnInfo>();
        }

        public void Dispose()
        {
            _currentWave.Dispose();
            _enemiesThisWave.Dispose();
            _targetKillsThisWave.Dispose();
            _enemiesKilled.Dispose();
            _requiredBossKills.Dispose();
            _bossKills.Dispose();
            _isAllWavesCleared.Dispose();
            _onWaveCleared.Dispose();
            _onKillCounted.Dispose();
        }
    }

    /// <summary>
    /// ウェーブスポーン情報
    /// </summary>
    public class WaveSpawnInfo
    {
        public int WaveId { get; set; }
        public int WaveNumber { get; set; }
        /// <summary>総スポーン数</summary>
        public int EnemyCount { get; set; }
        /// <summary>クリア目標数</summary>
        public int TargetKillCount { get; set; }
        /// <summary>必要ボス撃破数</summary>
        public int RequiredBossKills { get; set; }
        public float SpawnInterval { get; set; }
        public float EnemySpeedMultiplier { get; set; }
        public float EnemyHealthMultiplier { get; set; }
        public float EnemyDamageMultiplier { get; set; }
        public float ExperienceMultiplier { get; set; }
        /// <summary>スコア倍率（Waveクリア時: 残り時間 × ScoreMultiplier）</summary>
        public int ScoreMultiplier { get; set; }
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
        /// <summary>アイテムドロップグループID（0=ドロップなし）</summary>
        public int ItemDropGroupId { get; set; }
        /// <summary>経験値ドロップグループID（0=ドロップなし）</summary>
        public int ExpDropGroupId { get; set; }
    }
}
