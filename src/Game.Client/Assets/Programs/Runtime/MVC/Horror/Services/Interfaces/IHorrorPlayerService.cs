using Game.Shared.Scriptable.Database.Tables;
using Game.Shared.Services.Interfaces;

namespace Game.Horror.Services.Interfaces
{
    /// <summary>
    /// Horror プレイヤー状態（操作対象のマスター・HP 等）を扱うドメインサービスのインターフェース。
    /// </summary>
    public interface IHorrorPlayerService : IGameService
    {
        /// <summary>操作するプレイヤーの Id（未ロード時は既定 Id）。</summary>
        int PlayerId { get; }

        /// <summary>
        /// 現在プレイ中のプレイヤーマスター。<see cref="PlayerId"/> のマスターが不在なら既定 Id へフォールバックし、
        /// それも不在なら null（マスターデータ側の不備）。
        /// </summary>
        HorrorPlayerMaster PlayerMaster { get; }

        /// <summary>残 HP（0 = 未記録・未ロード。復元側で最大 HP へ正規化する）。</summary>
        int CurrentHealth { get; }

        /// <summary>残 HP を記録する。未ロード時は LogError の上で何もしない。同値の場合は何もしない。</summary>
        void SetCurrentHealth(int health);

        /// <summary>最大 HP（0 = マスター未解決）。<see cref="PlayerMaster"/> 由来のランタイム値でセーブデータには含まれない。</summary>
        int MaxHealth { get; }

        /// <summary>HP が満タンで回復アイテムを使用できないか。MaxHealth 未解決（0 以下）は満タン扱いにしない。</summary>
        bool IsHealthFull { get; }
    }
}
