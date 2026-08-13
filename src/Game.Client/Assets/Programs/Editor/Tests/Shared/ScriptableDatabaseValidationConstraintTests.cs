using System;
using System.Collections.Generic;
using System.Linq;
using Game.Shared.Scriptable.Database;
using Game.Shared.Scriptable.Database.Validation;
using NUnit.Framework;

namespace Game.Tests.Shared
{
    /// <summary>
    /// 検証属性（値制約・一意性）の単体テスト。ScriptableDatabase 資産には依存しない。
    /// テスト用レコード型に [ScriptableTable] は付けない（本物のテーブル走査に混ざるため）。
    /// </summary>
    public class ScriptableDatabaseValidationConstraintTests
    {
        // ---- テスト用レコード ----

        // 各制約を 1 つずつ持たせ、テストごとに 1 項目だけ違反させて使う。
        private sealed class ConstrainedRecord
        {
            [PrimaryKey] public int Id { get; set; }

            [StringNotNull] public string Name { get; set; }

            [StringNotNull(AllowEmpty = true)] public string OptionalName { get; set; }

            [ValueRange(0, 1)] public float Ratio { get; set; }

            [ValueRange(1, 99)] public int Count { get; set; }

            [StringLength(4, MinimumLength = 2)] public string Code { get; set; }

            [RegularExpression("^[a-z]+$")] public string Tag { get; set; }

            [Unique] public string UniqueName { get; set; }
        }

        private sealed class CompareRecord
        {
            [PrimaryKey] public int Id { get; set; }

            public int Threshold { get; set; }

            [Compare(nameof(Threshold), CompareOperator.Equal)] public int Equal { get; set; }

            [Compare(nameof(Threshold), CompareOperator.NotEqual)] public int NotEqual { get; set; }

            [Compare(nameof(Threshold), CompareOperator.LessThan)] public int LessThan { get; set; }

            [Compare(nameof(Threshold), CompareOperator.LessThanOrEqual)] public int LessThanOrEqual { get; set; }

            [Compare(nameof(Threshold), CompareOperator.GreaterThan)] public int GreaterThan { get; set; }

            [Compare(nameof(Threshold), CompareOperator.GreaterThanOrEqual)] public int GreaterThanOrEqual { get; set; }
        }

        private sealed class InvalidDeclarationRecord
        {
            [PrimaryKey] public int Id { get; set; }

            [StringNotNull] public int NotAString { get; set; }

            [ValueRange(0, 1)] public string NotANumber { get; set; }

            [ValueRange(1, 0)] public int ReversedRange { get; set; }

            [StringLength(2, MinimumLength = 5)] public string ReversedLength { get; set; }

            [RegularExpression("[")] public string BrokenPattern { get; set; }

            [Compare("NotDeclared", CompareOperator.Equal)] public int MissingOther { get; set; }

            [Compare(nameof(OtherTypeMember), CompareOperator.Equal)] public int TypeMismatch { get; set; }

            public string OtherTypeMember { get; set; }

            [Compare(nameof(SelfCompare), CompareOperator.Equal)] public int SelfCompare { get; set; }
        }

        private sealed class NoPrimaryKeyConstrainedRecord
        {
            [StringNotNull] public string Name { get; set; }
        }

        // ---- 実行ヘルパ ----

        private static ConstrainedRecord Valid(int id = 1) => new()
        {
            Id = id,
            Name = "name",
            OptionalName = string.Empty,
            Ratio = 0.5f,
            Count = 1,
            Code = "abc",
            Tag = "abc",
            UniqueName = "unique" + id,
        };

        private static CompareRecord ValidCompareRecord() => new()
        {
            Id = 1,
            Threshold = 10,
            Equal = 10,
            NotEqual = 11,
            LessThan = 9,
            LessThanOrEqual = 10,
            GreaterThan = 11,
            GreaterThanOrEqual = 10,
        };

        private static ValidationResult Execute<TRecord>(params TRecord[] records)
        {
            var getter = new StubRecordGetter();
            getter.Add(records);

            return ValidationExecutor.Create(new[] { typeof(TRecord) }, getter).Execute<TRecord>();
        }

        private static IReadOnlyList<string> ConfigurationErrors<TRecord>()
        {
            var executor = ValidationExecutor.Create(new[] { typeof(TRecord) }, new StubRecordGetter());

            return executor.ConfigurationResult.Errors.TryGetValue(typeof(TRecord).Name, out var messages)
                ? messages
                : Array.Empty<string>();
        }

        // ---- StringNotNull ----

        [Test]
        public void StringNotNull_ValidValue_HasNoErrors()
        {
            Assert.IsFalse(Execute(Valid()).HasErrors);
        }

        [TestCase((string)null)]
        [TestCase("")]
        public void StringNotNull_NullOrEmpty_ReportsError(string value)
        {
            var record = Valid();
            record.Name = value;

            var result = Execute(record);

            Assert.IsTrue(result.HasErrors);
            StringAssert.Contains(nameof(ConstrainedRecord.Name), result.Errors["1"][0]);
        }

        [Test]
        public void StringNotNull_AllowEmpty_AcceptsEmptyString()
        {
            var record = Valid();
            record.OptionalName = string.Empty;

            Assert.IsFalse(Execute(record).HasErrors);
        }

        [Test]
        public void StringNotNull_AllowEmpty_StillRejectsNull()
        {
            var record = Valid();
            record.OptionalName = null;

            Assert.IsTrue(Execute(record).HasErrors, "AllowEmpty が許すのは空文字であって未設定ではない。");
        }

        // ---- ValueRange ----

        [TestCase(-0.1f, true)]
        [TestCase(0f, false)]
        [TestCase(1f, false)]
        [TestCase(1.1f, true)]
        public void ValueRange_IncludesBothEnds(float value, bool expectsError)
        {
            var record = Valid();
            record.Ratio = value;

            Assert.AreEqual(expectsError, Execute(record).HasErrors);
        }

        [TestCase(0, true)]
        [TestCase(1, false)]
        [TestCase(99, false)]
        [TestCase(100, true)]
        public void ValueRange_AppliesToIntegerMembers(int value, bool expectsError)
        {
            var record = Valid();
            record.Count = value;

            Assert.AreEqual(expectsError, Execute(record).HasErrors);
        }

        // ---- StringLength ----

        [TestCase("a", true)]
        [TestCase("ab", false)]
        [TestCase("abcd", false)]
        [TestCase("abcde", true)]
        [TestCase(null, false)]
        public void StringLength_ChecksBothEndsAndIgnoresNull(string value, bool expectsError)
        {
            var record = Valid();
            record.Code = value;

            Assert.AreEqual(expectsError, Execute(record).HasErrors);
        }

        // ---- RegularExpression ----

        [TestCase("abc", false)]
        [TestCase("ABC", true)]
        [TestCase("a1", true)]
        [TestCase(null, false)]
        public void RegularExpression_MatchesPatternAndIgnoresNull(string value, bool expectsError)
        {
            var record = Valid();
            record.Tag = value;

            Assert.AreEqual(expectsError, Execute(record).HasErrors);
        }

        // ---- Compare ----

        [Test]
        public void Compare_SatisfyingValues_HasNoErrors()
        {
            Assert.IsFalse(Execute(ValidCompareRecord()).HasErrors);
        }

        [TestCase(nameof(CompareRecord.Equal), 11)]
        [TestCase(nameof(CompareRecord.NotEqual), 10)]
        [TestCase(nameof(CompareRecord.LessThan), 10)]
        [TestCase(nameof(CompareRecord.LessThanOrEqual), 11)]
        [TestCase(nameof(CompareRecord.GreaterThan), 10)]
        [TestCase(nameof(CompareRecord.GreaterThanOrEqual), 9)]
        public void Compare_EachOperator_RejectsViolatingValue(string memberName, int violatingValue)
        {
            var record = ValidCompareRecord();
            typeof(CompareRecord).GetProperty(memberName).SetValue(record, violatingValue);

            var result = Execute(record);

            Assert.IsTrue(result.HasErrors, $"{memberName}={violatingValue} は宣言違反であること。");
            StringAssert.Contains(memberName, result.Errors["1"][0]);
        }

        // ---- Unique ----

        [Test]
        public void Unique_DistinctValues_HasNoErrors()
        {
            Assert.IsFalse(Execute(Valid(1), Valid(2)).HasErrors);
        }

        [Test]
        public void Unique_DuplicatedValue_ReportsSecondOccurrence()
        {
            var first = Valid(1);
            var second = Valid(2);
            second.UniqueName = first.UniqueName;

            var result = Execute(first, second);

            Assert.IsTrue(result.Errors.ContainsKey("2"), "重複は 2 件目のレコードで報告する。");
            Assert.IsFalse(result.Errors.ContainsKey("1"), "最初に現れたレコードは重複扱いにしない。");
            StringAssert.Contains(nameof(ConstrainedRecord.UniqueName), result.Errors["2"][0]);
        }

        [Test]
        public void Unique_NullValues_AreNotDuplicates()
        {
            var first = Valid(1);
            var second = Valid(2);
            first.UniqueName = null;
            second.UniqueName = null;

            Assert.IsFalse(Execute(first, second).HasErrors, "未設定は重複判定の対象にしない。");
        }

        // ---- 構成チェック ----

        [TestCase(nameof(InvalidDeclarationRecord.NotAString), "[StringNotNull]")]
        [TestCase(nameof(InvalidDeclarationRecord.NotANumber), "[ValueRange]")]
        [TestCase(nameof(InvalidDeclarationRecord.ReversedRange), "[ValueRange]")]
        [TestCase(nameof(InvalidDeclarationRecord.ReversedLength), "[StringLength]")]
        [TestCase(nameof(InvalidDeclarationRecord.BrokenPattern), "[RegularExpression]")]
        [TestCase(nameof(InvalidDeclarationRecord.MissingOther), "[Compare]")]
        [TestCase(nameof(InvalidDeclarationRecord.TypeMismatch), "[Compare]")]
        [TestCase(nameof(InvalidDeclarationRecord.SelfCompare), "[Compare]")]
        public void Configuration_InvalidDeclaration_ReportsMemberAndAttribute(string memberName, string attributeName)
        {
            var messages = ConfigurationErrors<InvalidDeclarationRecord>();

            Assert.IsTrue(messages.Any(m => m.Contains(memberName) && m.Contains(attributeName)),
                $"{memberName} の {attributeName} が構成エラーになっていない:\n{string.Join("\n", messages)}");
        }

        [Test]
        public void Configuration_ConstraintWithoutPrimaryKey_ReportsError()
        {
            var messages = ConfigurationErrors<NoPrimaryKeyConstrainedRecord>();

            Assert.IsNotEmpty(messages, "エラー箇所の特定に主キーを使うため、検証属性には [PrimaryKey] が要る。");
        }

        [Test]
        public void Configuration_ValidDeclarations_HasNoErrors()
        {
            Assert.IsEmpty(ConfigurationErrors<ConstrainedRecord>());
            Assert.IsEmpty(ConfigurationErrors<CompareRecord>());
        }
    }
}
