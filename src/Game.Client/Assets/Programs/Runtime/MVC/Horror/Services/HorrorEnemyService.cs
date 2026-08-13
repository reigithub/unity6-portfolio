using System;
using System.Collections.Generic;
using Game.Core.Services;
using Game.Horror.Services.Interfaces;
using Game.Horror.Signals;
using Game.Shared.Services;
using UnityEngine;

namespace Game.Horror.Services
{
    /// <summary>
    /// Horror のエネミー撃破記録（撃破済み判定）とスポーングループ進行（全滅/キル数連鎖・トリガー発火）を扱うドメインサービス。
    /// </summary>
    public class HorrorEnemyService : IHorrorEnemyService
    {
        private readonly IHorrorSaveRepository _repository;
        private readonly IMessagePipeService _messagePipeService;
        private readonly IScriptableDatabaseService _databaseService;
        private IDisposable _subscription;

        // 起動済みスポーングループ（発火の一度きり保証・循環参照の暴走防止）。
        // 永続化せず、シーン開始時の GetActiveSpawnGroupIds() が撃破記録から fixpoint で再導出して置き換える
        private readonly HashSet<int> _activatedSpawnGroupIds = new();

        public HorrorEnemyService(
            IHorrorSaveRepository repository,
            IScriptableDatabaseService databaseService,
            IMessagePipeService messagePipeService)
        {
            _repository = repository;
            _databaseService = databaseService;
            _messagePipeService = messagePipeService;
        }

        public void Startup()
        {
            _subscription = _messagePipeService.Subscribe<HorrorSignals.Enemy.Died>(evt => OnEnemyDied(evt.SpawnId));
        }

        public void Shutdown()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        public bool IsDefeated(int spawnId)
        {
            // 未ロード時は無音で false（敵を出す方向へフェイルオープン。シーン構築中にログを撒かない）
            var data = _repository.Data?.Enemy;
            return data != null && data.DefeatedSpawnIds.Contains(spawnId);
        }

        public bool IsTriggerFired(int triggerId)
        {
            // 未ロード時は無音で false（IsDefeated と同じフェイルオープン）
            var data = _repository.Data?.Enemy;
            return data != null && data.FiredTriggerIds.Contains(triggerId);
        }

        public void NotifyTriggerPassed(int triggerId)
        {
            var data = _repository.Data?.Enemy;
            if (data == null)
            {
                // 記録の消失は「リロードでトリガーが再発火する」としか観測できないため、無音で落とさず顕在化させる
                Debug.LogError($"[{GetType().Name}] セーブデータ未ロードのためトリガー発火記録 (TriggerId={triggerId}) を無視しました");
                return;
            }

            if (data.FiredTriggerIds.Contains(triggerId)) return;

            // triggerId はシーンの SerializeField 由来のランタイム値（シーン×マスタの突き合わせはランタイムで検出する）
            if (!_databaseService.Database.HorrorEnemySpawnTriggerMasterTable.TryFindById(triggerId, out var trigger))
            {
                Debug.LogError($"[{GetType().Name}] HorrorEnemySpawnTriggerMaster (Id={triggerId}) が見つからないためトリガー発火を無視しました");
                return;
            }

            // グループが既に活性でも発火記録だけは残す（起動の一度きりは TryActivateSpawnGroup が保証）
            data.FiredTriggerIds.Add(triggerId);
            _repository.MarkDirty();
            TryActivateSpawnGroup(trigger.SpawnGroupId);
        }

        public int GetDefeatedCount(int spawnGroupId)
        {
            var data = _repository.Data?.Enemy;
            if (data == null) return 0;

            var records = _databaseService.Database.HorrorEnemySpawnMasterTable.FindBySpawnGroupId(spawnGroupId);
            var count = 0;
            foreach (var master in records)
            {
                if (data.DefeatedSpawnIds.Contains(master.Id)) count++;
            }

            return count;
        }

        public bool IsSpawnGroupEliminated(int spawnGroupId)
        {
            var records = _databaseService.Database.HorrorEnemySpawnMasterTable.FindBySpawnGroupId(spawnGroupId);
            if (records.Count == 0) return false; // 空集合を全滅扱いにすると、起動した瞬間に空のまま連鎖が走るため

            var data = _repository.Data?.Enemy;
            if (data == null) return false;

            foreach (var master in records)
            {
                if (!data.DefeatedSpawnIds.Contains(master.Id)) return false;
            }

            return true;
        }

        public IReadOnlyCollection<int> GetActiveSpawnGroupIds()
        {
            _activatedSpawnGroupIds.Clear();

            foreach (var spawnGroup in _databaseService.Database.HorrorEnemySpawnGroupMasterTable.All)
            {
                if (spawnGroup.IsInitialSpawn) _activatedSpawnGroupIds.Add(spawnGroup.Id);
            }

            // 発火済みトリガーの起動先も種に含める（トリガー起動は撃破記録から導出できないため FiredTriggerIds が正本）
            var enemyData = _repository.Data?.Enemy;
            if (enemyData != null)
            {
                foreach (var triggerId in enemyData.FiredTriggerIds)
                {
                    if (_databaseService.Database.HorrorEnemySpawnTriggerMasterTable.TryFindById(triggerId, out var trigger))
                        _activatedSpawnGroupIds.Add(trigger.SpawnGroupId);
                }
            }

            // 全滅/閾値の連鎖を集合が安定するまで反復し、セーブデータ由来の途中状態を再構築する
            bool changed;
            do
            {
                changed = false;
                foreach (var spawnGroup in _databaseService.Database.HorrorEnemySpawnGroupMasterTable.All)
                {
                    if (!_activatedSpawnGroupIds.Contains(spawnGroup.Id)) continue;

                    if (spawnGroup.AdditionalGroupId != 0 && spawnGroup.AdditionalKillThreshold > 0 &&
                        GetDefeatedCount(spawnGroup.Id) >= spawnGroup.AdditionalKillThreshold &&
                        _activatedSpawnGroupIds.Add(spawnGroup.AdditionalGroupId))
                    {
                        changed = true;
                    }

                    if (spawnGroup.NextGroupIdOnEliminated != 0 && IsSpawnGroupEliminated(spawnGroup.Id) &&
                        _activatedSpawnGroupIds.Add(spawnGroup.NextGroupIdOnEliminated))
                    {
                        changed = true;
                    }
                }
            }
            while (changed);

            // 内部集合はランタイム連鎖で増え続けるため、呼び出し時点のスナップショットを返す
            return new HashSet<int>(_activatedSpawnGroupIds);
        }

        private void OnEnemyDied(int spawnId)
        {
            // 進行判定は記録が新規に行われたときのみ（冪等 no-op や不正 Id で連鎖判定を走らせない）
            if (!TryMarkDefeated(spawnId)) return;
            EvaluateSpawnGroupProgression(spawnId);
        }

        private bool TryMarkDefeated(int spawnId)
        {
            var data = _repository.Data?.Enemy;
            if (data == null)
            {
                // 記録の消失は「リロードで敵が復活する」としか観測できないため、無音で落とさず顕在化させる
                Debug.LogError($"[{GetType().Name}] セーブデータ未ロードのため撃破記録 (SpawnId={spawnId}) を無視しました");
                return false;
            }

            if (spawnId <= 0)
            {
                Debug.LogError($"[{GetType().Name}] 無効なスポーン Id のため撃破記録 (SpawnId={spawnId}) を無視しました");
                return false;
            }

            if (data.DefeatedSpawnIds.Contains(spawnId)) return false;

            data.DefeatedSpawnIds.Add(spawnId);
            _repository.MarkDirty();
            return true;
        }

        private void EvaluateSpawnGroupProgression(int spawnId)
        {
            var database = _databaseService.Database;
            if (!database.HorrorEnemySpawnMasterTable.TryFindById(spawnId, out var spawn))
            {
                Debug.LogError($"[{GetType().Name}] HorrorEnemySpawnMaster (Id={spawnId}) が見つからないためスポーングループ進行判定をスキップしました");
                return;
            }

            if (!database.HorrorEnemySpawnGroupMasterTable.TryFindById(spawn.SpawnGroupId, out var spawnGroup)) return;

            // 同一キルで閾値と全滅の両方が成立したら両方起動する
            if (spawnGroup.AdditionalGroupId != 0 && spawnGroup.AdditionalKillThreshold > 0 &&
                GetDefeatedCount(spawnGroup.Id) >= spawnGroup.AdditionalKillThreshold)
            {
                TryActivateSpawnGroup(spawnGroup.AdditionalGroupId);
            }

            if (spawnGroup.NextGroupIdOnEliminated != 0 && IsSpawnGroupEliminated(spawnGroup.Id))
            {
                TryActivateSpawnGroup(spawnGroup.NextGroupIdOnEliminated);
            }
        }

        private void TryActivateSpawnGroup(int spawnGroupId)
        {
            if (!_activatedSpawnGroupIds.Add(spawnGroupId)) return; // 起動済み（閾値は到達後の毎キルで成立し続けるため、ここで一度きりを保証）
            _messagePipeService.Publish(new HorrorSignals.Enemy.SpawnGroupActivated(spawnGroupId));
        }
    }
}
