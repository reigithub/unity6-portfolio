using Game.Horror.Services.Interfaces;
using UnityEngine;

namespace Game.Horror.Services
{
    /// <summary>
    /// Horror のエネミー撃破記録（撃破済み判定）を扱うドメインサービス。
    /// </summary>
    public class HorrorEnemyService : IHorrorEnemyService
    {
        private readonly IHorrorSaveRepository _repository;

        public HorrorEnemyService(IHorrorSaveRepository repository)
        {
            _repository = repository;
        }

        public void MarkDefeated(int spawnId)
        {
            var data = _repository.Data?.Enemy;
            if (data == null)
            {
                // 記録の消失は「リロードで敵が復活する」としか観測できないため、無音で落とさず顕在化させる
                Debug.LogError($"[{GetType().Name}] セーブデータ未ロードのため {nameof(MarkDefeated)}({spawnId}) を無視しました");
                return;
            }

            if (spawnId <= 0)
            {
                Debug.LogError($"[{GetType().Name}] 無効なスポーン Id のため {nameof(MarkDefeated)}({spawnId}) を無視しました");
                return;
            }

            if (data.DefeatedSpawnIds.Contains(spawnId)) return;

            data.DefeatedSpawnIds.Add(spawnId);
            _repository.MarkDirty();
        }

        public bool IsDefeated(int spawnId)
        {
            // 未ロード時は無音で false（敵を出す方向へフェイルオープン。シーン構築中にログを撒かない）
            var data = _repository.Data?.Enemy;
            return data != null && data.DefeatedSpawnIds.Contains(spawnId);
        }
    }
}
