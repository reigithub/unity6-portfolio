#if UNITY_EDITOR
using System;
using System.Reflection;

namespace Game.Shared.Scriptable.Database.Validation
{
    /// <summary><see cref="ValueRangeAttribute"/> の宣言から作られる数値範囲チェック（両端を含む）。</summary>
    internal sealed class ValueRangeRecordValidator<TRecord> : IRecordValidator<TRecord>
    {
        private readonly MemberInfo _member;
        private readonly MemberInfo _primaryKeyMember;
        private readonly double _minimum;
        private readonly double _maximum;

        public ValueRangeRecordValidator(MemberInfo member, MemberInfo primaryKeyMember, ValueRangeAttribute attribute)
        {
            _member = member;
            _primaryKeyMember = primaryKeyMember;
            _minimum = attribute.Minimum;
            _maximum = attribute.Maximum;
        }

        public void Validate(TRecord record, ValidationResult result, IRecordGetter recordGetter)
        {
            // 表示は元の値を使う（float を double へ広げると 0.1f が 0.100000001… になり読みにくいため）。
            object raw = ValidationReflection.GetValue(_member, record);
            double value = Convert.ToDouble(raw);
            if (value >= _minimum && value <= _maximum) return;

            result.AddError(
                ValidationReflection.GetValue(_primaryKeyMember, record).ToString(),
                $"{_member.Name}={raw} が範囲 {_minimum}〜{_maximum} の外です。");
        }
    }

    internal static class ValueRangeRecordValidator
    {
        public static object Create(
            Type recordType, MemberInfo member, MemberInfo primaryKey, ValueRangeAttribute attribute, ValidationResult result)
        {
            if (!DeclaredValidators.RequireMemberType(recordType, member, "[ValueRange]", result,
                    typeof(int), typeof(long), typeof(float), typeof(double)))
            {
                return null;
            }

            if (attribute.Minimum > attribute.Maximum)
            {
                result.AddError(recordType.Name,
                    $"{DeclaredValidators.Describe(recordType, member)} の [ValueRange] は下限 {attribute.Minimum} が上限 {attribute.Maximum} を超えています。");
                return null;
            }

            return DeclaredValidators.Create(typeof(ValueRangeRecordValidator<>), recordType, member, primaryKey, attribute);
        }
    }
}
#endif
