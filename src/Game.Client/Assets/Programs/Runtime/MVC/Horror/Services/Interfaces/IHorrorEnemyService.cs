using Game.Shared.Services.Interfaces;

namespace Game.Horror.Services.Interfaces
{
    /// <summary>
    /// Horror のエネミー撃破記録（撃破済み判定）を扱うドメインサービスのインターフェース。
    /// </summary>
    public interface IHorrorEnemyService : IGameService
    {
        /// <summary>撃破を記録する。未記録なら追加して Dirty にする。</summary>
        void MarkDefeated(int spawnId);

        /// <summary>指定スポーン Id（HorrorEnemySpawnMaster の Id）が撃破済みか判定する。</summary>
        bool IsDefeated(int spawnId);
    }
}
