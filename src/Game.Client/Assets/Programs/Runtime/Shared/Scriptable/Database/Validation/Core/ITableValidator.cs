#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Game.Shared.Scriptable.Database.Validation
{
    /// <summary>レコード型を実行時に扱うための非ジェネリック面。</summary>
    public interface ITableValidator
    {
        Type RecordType { get; }
    }

    /// <summary>
    /// テーブル 1 つ分の検証入口。実装は <see cref="TableValidator{TRecord}"/> の派生に限る
    /// （直接実装すると <see cref="IRecordValidator{TRecord}"/> 群・<see cref="IRecordsValidator{TRecord}"/> 群の
    /// 実行契約を満たせないため、<see cref="ValidationExecutor"/> が構成エラーとして検出する）。
    /// </summary>
    public interface ITableValidator<TRecord> : ITableValidator
    {
        ValidationResult ValidateAll(
            IReadOnlyList<IRecordValidator<TRecord>> recordValidators,
            IReadOnlyList<IRecordsValidator<TRecord>> recordsValidators,
            IRecordGetter recordGetter);
    }
}
#endif
