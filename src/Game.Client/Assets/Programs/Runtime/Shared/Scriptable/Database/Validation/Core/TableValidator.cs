#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Game.Shared.Scriptable.Database.Validation
{
    /// <summary>
    /// テーブル検証の既定実装。<see cref="ValidateAll(IReadOnlyList{TRecord}, ValidationResult, IRecordGetter)"/> を
    /// override すればテーブル横断の検証を、<see cref="Validate(TRecord, ValidationResult, IRecordGetter)"/> を
    /// override すればレコード単位の検証を C# で自由に記述できる。
    /// 派生クラスを作らなくても <see cref="ValidationExecutor"/> が全テーブルへ既定インスタンスを補完するため、
    /// <see cref="IRecordValidator{TRecord}"/> や <see cref="IRecordsValidator{TRecord}"/> の実装だけでも足りる。
    /// </summary>
    public class TableValidator<TRecord> : ITableValidator<TRecord>
    {
        public Type RecordType => typeof(TRecord);

        /// <summary>テーブル全体を見る検証（重複・連番の欠落など）。</summary>
        protected virtual void ValidateAll(IReadOnlyList<TRecord> allRecords, ValidationResult result, IRecordGetter recordGetter)
        {
        }

        /// <summary>レコード単位の検証。</summary>
        protected virtual void Validate(TRecord record, ValidationResult result, IRecordGetter recordGetter)
        {
        }

        // 非 virtual。派生が実行順序を差し替えて validator 群を素通りさせられないようにする。
        public ValidationResult ValidateAll(
            IReadOnlyList<IRecordValidator<TRecord>> recordValidators,
            IReadOnlyList<IRecordsValidator<TRecord>> recordsValidators,
            IRecordGetter recordGetter)
        {
            var allRecords = recordGetter.GetAll<TRecord>();
            var result = new ValidationResult(typeof(TRecord).Name, allRecords.Count);

            ValidateAll(allRecords, result, recordGetter);

            for (int i = 0; i < recordsValidators.Count; i++)
            {
                recordsValidators[i].Validate(allRecords, result, recordGetter);
            }

            for (int i = 0; i < allRecords.Count; i++)
            {
                var record = allRecords[i];

                // レコード単位の validator は null を前提にしていないため、報告だけしてこのレコードは飛ばす
                // （1 件の null で例外を投げ、テーブル全体の検証結果を捨てないようにする）。
                if (record is null)
                {
                    result.AddError($"index {i}", "レコードが null です。");
                    continue;
                }

                Validate(record, result, recordGetter);
                for (int j = 0; j < recordValidators.Count; j++)
                {
                    recordValidators[j].Validate(record, result, recordGetter);
                }
            }

            return result;
        }
    }
}
#endif
