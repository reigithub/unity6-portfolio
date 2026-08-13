using System.Collections.Generic;
using Game.Shared.Services.Interfaces;

namespace Game.Horror.Services.Interfaces
{
    /// <summary>
    /// Horror のエネミー撃破記録（撃破済み判定）とスポーングループ進行（全滅/キル数連鎖・トリガー発火）を扱うドメインサービスのインターフェース。
    /// </summary>
    public interface IHorrorEnemyService : IGameService
    {
        /// <summary>指定スポーン Id（HorrorEnemySpawnMaster の Id）が撃破済みか判定する。</summary>
        bool IsDefeated(int spawnId);

        /// <summary>指定トリガー Id（HorrorEnemySpawnTriggerMaster の Id）が発火済みか判定する。</summary>
        bool IsTriggerFired(int triggerId);

        /// <summary>
        /// トリガー通過を通知する。未発火なら発火を記録し、対応するスポーングループを起動する（発火済みは冪等 no-op）。
        /// </summary>
        void NotifyTriggerPassed(int triggerId);

        /// <summary>指定スポーングループ（HorrorEnemySpawnGroupMaster の Id）の撃破済み所属エントリ数を返す。</summary>
        int GetDefeatedCount(int spawnGroupId);

        /// <summary>指定スポーングループの所属エントリが全滅しているか判定する。所属0件のグループは false。</summary>
        bool IsSpawnGroupEliminated(int spawnGroupId);

        /// <summary>
        /// 撃破記録から活性スポーングループ集合を再計算して返す（初期グループを種に全滅/閾値連鎖を安定するまで反復）。
        /// シーン開始時に呼ぶこと。以後のランタイム連鎖の発火済みガードもこの集合を引き継ぐ。
        /// </summary>
        IReadOnlyCollection<int> GetActiveSpawnGroupIds();
    }
}
