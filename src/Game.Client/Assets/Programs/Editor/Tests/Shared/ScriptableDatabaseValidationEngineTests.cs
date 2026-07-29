using System;
using System.Collections.Generic;
using System.Linq;
using Game.Shared.Scriptable.Database;
using Game.Shared.Scriptable.Database.Validation;
using NUnit.Framework;

namespace Game.Tests.Shared
{
    /// <summary>
    /// 検証エンジンの単体テスト。ScriptableDatabase 資産に依存しないよう、
    /// レコード供給をスタブに差し替えて実行する。
    /// テスト用レコード型に [ScriptableTable] は付けない（本物のテーブル走査に混ざるため）。
    /// </summary>
    public class ScriptableDatabaseValidationEngineTests
    {
        // ---- テスト用レコード ----

        private sealed class ParentRecord
        {
            [PrimaryKey] public int Id { get; set; }
        }

        private sealed class ChildRecord
        {
            [PrimaryKey] public int Id { get; set; }

            [ForeignKey(typeof(ParentRecord))] public int ParentId { get; set; }

            [ForeignKey(typeof(ParentRecord), AllowNone = true)] public int OptionalParentId { get; set; }
        }

        private sealed class NoPrimaryKeyRecord
        {
            [ForeignKey(typeof(ParentRecord))] public int ParentId { get; set; }
        }

        private sealed class NonIntForeignKeyRecord
        {
            [PrimaryKey] public int Id { get; set; }

            [ForeignKey(typeof(ParentRecord))] public string ParentName { get; set; }
        }

        // ---- テスト用スタブ ----

        private sealed class StubRecordGetter : IRecordGetter
        {
            private readonly Dictionary<Type, List<object>> _records = new();

            public void Add<TRecord>(params TRecord[] records) =>
                _records[typeof(TRecord)] = records.Cast<object>().ToList();

            public IReadOnlyList<TRecord> GetAll<TRecord>() => Records(typeof(TRecord)).Cast<TRecord>().ToList();

            public bool ContainsPrimaryKey(Type targetRecordType, int primaryKey)
            {
                var property = targetRecordType.GetProperty("Id");
                return Records(targetRecordType).Any(r => (int)property.GetValue(r) == primaryKey);
            }

            private List<object> Records(Type recordType)
            {
                if (_records.TryGetValue(recordType, out var records)) return records;

                throw new InvalidOperationException($"{recordType.Name} は登録されていません。");
            }
        }

        private sealed class ThrowingTableValidator : TableValidator<ParentRecord>
        {
            public const string Message = "テーブル検証で発生した例外";

            protected override void ValidateAll(IReadOnlyList<ParentRecord> allRecords, ValidationResult result, IRecordGetter recordGetter) =>
                throw new InvalidOperationException(Message);
        }

        private sealed class CountingRecordValidator : IRecordValidator<ParentRecord>
        {
            public int Calls { get; private set; }

            public void Validate(ParentRecord record, ValidationResult result, IRecordGetter recordGetter) => Calls++;
        }

        private static StubRecordGetter CreateGetter(params ChildRecord[] children)
        {
            var getter = new StubRecordGetter();
            getter.Add(new ParentRecord { Id = 1 }, new ParentRecord { Id = 2 });
            getter.Add(children);
            return getter;
        }

        private static ValidationExecutor CreateExecutor(StubRecordGetter getter, params object[] validators) =>
            ValidationExecutor.Create(new[] { typeof(ParentRecord), typeof(ChildRecord) }, getter, validators);

        // ---- 外部キー ----

        [Test]
        public void ForeignKey_ExistingReference_HasNoErrors()
        {
            var executor = CreateExecutor(CreateGetter(new ChildRecord { Id = 10, ParentId = 1, OptionalParentId = 2 }));

            Assert.IsFalse(executor.Execute<ChildRecord>().HasErrors);
        }

        [Test]
        public void ForeignKey_MissingReference_ReportsErrorKeyedByPrimaryKey()
        {
            var executor = CreateExecutor(CreateGetter(new ChildRecord { Id = 10, ParentId = 99 }));

            var result = executor.Execute<ChildRecord>();

            Assert.IsTrue(result.HasErrors);
            Assert.IsTrue(result.Errors.ContainsKey("10"), "エラーキーはレコードの主キー値であること。");
            StringAssert.Contains(nameof(ParentRecord), result.Errors["10"][0]);
        }

        [Test]
        public void ForeignKey_ZeroWithoutAllowNone_ReportsError()
        {
            var executor = CreateExecutor(CreateGetter(new ChildRecord { Id = 10, ParentId = 0, OptionalParentId = 1 }));

            var result = executor.Execute<ChildRecord>();

            Assert.IsTrue(result.HasErrors, "AllowNone 無指定の 0 は未設定として見逃さない。");
            StringAssert.Contains(nameof(ChildRecord.ParentId), result.Errors["10"][0]);
        }

        [Test]
        public void ForeignKey_ZeroWithAllowNone_HasNoErrors()
        {
            var executor = CreateExecutor(CreateGetter(new ChildRecord { Id = 10, ParentId = 1, OptionalParentId = 0 }));

            Assert.IsFalse(executor.Execute<ChildRecord>().HasErrors);
        }

        // ---- 構成チェック ----

        [Test]
        public void Configuration_ForeignKeyOnRecordWithoutPrimaryKey_ReportsError()
        {
            var executor = ValidationExecutor.Create(new[] { typeof(NoPrimaryKeyRecord) }, CreateGetter());

            Assert.IsTrue(executor.ConfigurationResult.HasErrors, "[PrimaryKey] が無ければ [ForeignKey] は成立しない。");
        }

        [Test]
        public void Configuration_ForeignKeyOnNonIntMember_ReportsError()
        {
            var executor = ValidationExecutor.Create(new[] { typeof(NonIntForeignKeyRecord) }, CreateGetter());

            Assert.IsTrue(executor.ConfigurationResult.HasErrors);
        }

        [Test]
        public void Configuration_ForeignKeyTargetOutOfScope_ReportsError()
        {
            // 参照先 ParentRecord を検証対象に含めない＝参照先テーブルが無い状態。
            var executor = ValidationExecutor.Create(new[] { typeof(ChildRecord) }, CreateGetter());

            Assert.IsTrue(executor.ConfigurationResult.HasErrors, "参照先テーブルが無い宣言は黙って無効化せずエラーにすること。");
        }

        // ---- 実行器 ----

        [Test]
        public void ExecuteAll_TableWithoutValidator_IsStillReportedWithRecordCount()
        {
            var executor = CreateExecutor(CreateGetter());

            var result = executor.ExecuteAll().Single(x => x.Name == nameof(ParentRecord));

            Assert.AreEqual(2, result.RecordCount, "validator が無いテーブルも件数付きで結果に出ること。");
            Assert.IsFalse(result.HasErrors);
        }

        [Test]
        public void ExecuteAll_StartsWithConfigurationResult()
        {
            var results = CreateExecutor(CreateGetter()).ExecuteAll();

            Assert.AreEqual(ValidationExecutor.ConfigurationResultName, results[0].Name);
            Assert.AreEqual(3, results.Count, "構成チェック＋テーブル 2 件。");
        }

        [Test]
        public void ExecuteAll_ValidatorThrows_IsFoldedIntoResultAndOtherTablesRun()
        {
            var executor = CreateExecutor(
                CreateGetter(new ChildRecord { Id = 10, ParentId = 1, OptionalParentId = 1 }),
                new ThrowingTableValidator());

            var results = executor.ExecuteAll();

            var thrown = results.Single(x => x.Name == nameof(ParentRecord));
            Assert.AreEqual(-1, thrown.RecordCount, "例外で中断したテーブルは RecordCount = -1。");
            StringAssert.Contains(ThrowingTableValidator.Message, thrown.Errors.Values.First()[0]);

            Assert.IsFalse(results.Single(x => x.Name == nameof(ChildRecord)).HasErrors, "他テーブルの検証は止まらないこと。");
        }

        [Test]
        public void Execute_RecordValidatorRunsForEveryRecord()
        {
            var validator = new CountingRecordValidator();

            CreateExecutor(CreateGetter(), validator).Execute<ParentRecord>();

            Assert.AreEqual(2, validator.Calls);
        }

        [Test]
        public void Execute_UnknownRecordType_Throws()
        {
            var executor = ValidationExecutor.Create(new[] { typeof(ParentRecord) }, CreateGetter());

            Assert.Throws<ArgumentException>(() => executor.Execute<ChildRecord>());
        }
    }
}
