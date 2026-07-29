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
    /// Horror のエネミー撃破記録（撃破済み判定）とグループ進行（全滅/キル数連鎖）を扱うドメインサービス。
    /// </summary>
    public class HorrorEnemyService : IHorrorEnemyService
    {
        private readonly IHorrorSaveRepository _repository;
        private readonly IMessagePipeService _messagePipeService;
        private readonly IScriptableDatabaseService _databaseService;
        private IDisposable _subscription;

        // 起動済みグループ（発火の一度きり保証・循環参照の暴走防止）。
        // 永続化せず、シーン開始時の GetActiveGroupIds() が撃破記録から fixpoint で再導出して置き換える
        private readonly HashSet<int> _activatedGroupIds = new();

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

        public int GetDefeatedCount(int groupId)
        {
            var data = _repository.Data?.Enemy;
            if (data == null) return 0;

            var records = _databaseService.Database.HorrorEnemySpawnMasterTable.FindByGroupId(groupId);
            var count = 0;
            foreach (var master in records)
            {
                if (data.DefeatedSpawnIds.Contains(master.Id)) count++;
            }

            return count;
        }

        public bool IsGroupEliminated(int groupId)
        {
            var records = _databaseService.Database.HorrorEnemySpawnMasterTable.FindByGroupId(groupId);
            if (records.Count == 0) return false; // 空集合を全滅扱いにすると、起動した瞬間に空のまま連鎖が走るため

            var data = _repository.Data?.Enemy;
            if (data == null) return false;

            foreach (var master in records)
            {
                if (!data.DefeatedSpawnIds.Contains(master.Id)) return false;
            }

            return true;
        }

        public IReadOnlyCollection<int> GetActiveGroupIds()
        {
            _activatedGroupIds.Clear();

            ValidateGroupMasters();

            foreach (var group in _databaseService.Database.HorrorEnemyGroupMasterTable.All)
            {
                if (group.IsInitialSpawn) _activatedGroupIds.Add(group.Id);
            }

            // 全滅/閾値の連鎖を集合が安定するまで反復し、セーブデータ由来の途中状態を再構築する
            bool changed;
            do
            {
                changed = false;
                foreach (var group in _databaseService.Database.HorrorEnemyGroupMasterTable.All)
                {
                    if (!_activatedGroupIds.Contains(group.Id)) continue;

                    if (group.AdditionalGroupId != 0 && group.AdditionalKillThreshold > 0 &&
                        GetDefeatedCount(group.Id) >= group.AdditionalKillThreshold &&
                        _activatedGroupIds.Add(group.AdditionalGroupId))
                    {
                        changed = true;
                    }

                    if (group.NextGroupIdOnEliminated != 0 && IsGroupEliminated(group.Id) &&
                        _activatedGroupIds.Add(group.NextGroupIdOnEliminated))
                    {
                        changed = true;
                    }
                }
            }
            while (changed);

            // 内部集合はランタイム連鎖で増え続けるため、呼び出し時点のスナップショットを返す
            return new HashSet<int>(_activatedGroupIds);
        }

        private void OnEnemyDied(int spawnId)
        {
            // 進行判定は記録が新規に行われたときのみ（冪等 no-op や不正 Id で連鎖判定を走らせない）
            if (!TryMarkDefeated(spawnId)) return;
            EvaluateGroupProgression(spawnId);
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

        private void EvaluateGroupProgression(int spawnId)
        {
            var database = _databaseService.Database;
            if (database == null)
            {
                Debug.LogError($"[{GetType().Name}] マスターデータ未ロードのためグループ進行判定 (SpawnId={spawnId}) をスキップしました");
                return;
            }

            if (!database.HorrorEnemySpawnMasterTable.TryFindById(spawnId, out var spawn))
            {
                Debug.LogError($"[{GetType().Name}] HorrorEnemySpawnMaster (Id={spawnId}) が見つからないためグループ進行判定をスキップしました");
                return;
            }

            if (!database.HorrorEnemyGroupMasterTable.TryFindById(spawn.GroupId, out var group))
            {
                Debug.LogError($"[{GetType().Name}] HorrorEnemyGroupMaster (Id={spawn.GroupId}) が見つからないためグループ進行判定をスキップしました");
                return;
            }

            // 同一キルで閾値と全滅の両方が成立したら両方起動する
            if (group.AdditionalGroupId != 0 && group.AdditionalKillThreshold > 0 &&
                GetDefeatedCount(group.Id) >= group.AdditionalKillThreshold)
            {
                TryActivateGroup(group.AdditionalGroupId);
            }

            if (group.NextGroupIdOnEliminated != 0 && IsGroupEliminated(group.Id))
            {
                TryActivateGroup(group.NextGroupIdOnEliminated);
            }
        }

        private void TryActivateGroup(int groupId)
        {
            if (!_activatedGroupIds.Add(groupId)) return; // 起動済み（閾値は到達後の毎キルで成立し続けるため、ここで一度きりを保証）
            _messagePipeService.Publish(new HorrorSignals.Enemy.GroupActivated(groupId));
        }

        /// <summary>
        /// グループマスタの整合性を検証する（毎シーン開始の決定点で LogError）。
        /// 参照先不在・閾値と追加グループの片設定・所属エントリ0件を検出する。
        /// </summary>
        private void ValidateGroupMasters()
        {
            var database = _databaseService.Database;
            var groupTable = database.HorrorEnemyGroupMasterTable;
            foreach (var group in groupTable.All)
            {
                if (group.NextGroupIdOnEliminated != 0 && !groupTable.TryFindById(group.NextGroupIdOnEliminated, out _))
                {
                    Debug.LogError($"[{GetType().Name}] HorrorEnemyGroupMaster (Id={group.Id}) の NextGroupIdOnEliminated={group.NextGroupIdOnEliminated} が見つかりません");
                }

                if (group.AdditionalGroupId != 0 && !groupTable.TryFindById(group.AdditionalGroupId, out _))
                {
                    Debug.LogError($"[{GetType().Name}] HorrorEnemyGroupMaster (Id={group.Id}) の AdditionalGroupId={group.AdditionalGroupId} が見つかりません");
                }

                if ((group.AdditionalGroupId != 0) != (group.AdditionalKillThreshold > 0))
                {
                    Debug.LogError($"[{GetType().Name}] HorrorEnemyGroupMaster (Id={group.Id}) の AdditionalKillThreshold と AdditionalGroupId は両方設定するか両方 0 にしてください (Threshold={group.AdditionalKillThreshold}, GroupId={group.AdditionalGroupId})");
                }

                if (database.HorrorEnemySpawnMasterTable.FindByGroupId(group.Id).Count == 0)
                {
                    Debug.LogError($"[{GetType().Name}] HorrorEnemyGroupMaster (Id={group.Id}) に所属するスポーンエントリがありません");
                }
            }
        }
    }
}
