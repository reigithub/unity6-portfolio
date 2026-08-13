#if UNITY_EDITOR
using System.Collections.Generic;

namespace Game.Shared.Scriptable.Database.Validation
{
    /// <summary>
    /// レコード 1 件では判定できない、テーブル全体にかかる検証（重複・連番の欠落など）。
    /// <see cref="IRecordValidator{TRecord}"/> と同じく、実装クラスが引数なしコンストラクタを持てば
    /// <see cref="ValidationExecutor"/> が自動発見して対象テーブルへ登録する。
    /// テーブル検証の入口である <see cref="ITableValidator{TRecord}"/> とは別物で、こちらは入口から呼ばれる側。
    /// </summary>
    public interface IRecordsValidator<TRecord>
    {
        /// <param name="allRecords">
        /// 全レコード。編集途中の null が混じり得るため、実装側で読み飛ばすこと
        /// （null 自体は <see cref="TableValidator{TRecord}"/> が別途エラーにする）。
        /// </param>
        void Validate(IReadOnlyList<TRecord> allRecords, ValidationResult result, IRecordGetter recordGetter);
    }
}
#endif
