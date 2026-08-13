#if UNITY_EDITOR
using System;
using System.Reflection;

namespace Game.Shared.Scriptable.Database.Validation
{
    /// <summary>
    /// <see cref="StringLengthAttribute"/> の宣言から作られる文字数チェック（両端を含む）。
    /// null は検証しない（<see cref="StringNotNullAttribute"/> の担当）。
    /// </summary>
    internal sealed class StringLengthRecordValidator<TRecord> : IRecordValidator<TRecord>
    {
        private readonly MemberInfo _member;
        private readonly MemberInfo _primaryKeyMember;
        private readonly int _minimumLength;
        private readonly int _maximumLength;

        public StringLengthRecordValidator(MemberInfo member, MemberInfo primaryKeyMember, StringLengthAttribute attribute)
        {
            _member = member;
            _primaryKeyMember = primaryKeyMember;
            _minimumLength = attribute.MinimumLength;
            _maximumLength = attribute.MaximumLength;
        }

        public void Validate(TRecord record, ValidationResult result, IRecordGetter recordGetter)
        {
            var value = (string)ValidationReflection.GetValue(_member, record);
            if (value == null) return;
            if (value.Length >= _minimumLength && value.Length <= _maximumLength) return;

            result.AddError(
                ValidationReflection.GetValue(_primaryKeyMember, record).ToString(),
                $"{_member.Name} の文字数 {value.Length} が範囲 {_minimumLength}〜{_maximumLength} の外です。");
        }
    }

    internal static class StringLengthRecordValidator
    {
        public static object Create(
            Type recordType, MemberInfo member, MemberInfo primaryKey, StringLengthAttribute attribute, ValidationResult result)
        {
            if (!DeclaredValidators.RequireMemberType(recordType, member, "[StringLength]", result, typeof(string))) return null;

            string where = DeclaredValidators.Describe(recordType, member);

            if (attribute.MinimumLength < 0 || attribute.MaximumLength < 0)
            {
                result.AddError(recordType.Name, $"{where} の [StringLength] に負の文字数が指定されています。");
                return null;
            }

            if (attribute.MinimumLength > attribute.MaximumLength)
            {
                result.AddError(recordType.Name,
                    $"{where} の [StringLength] は下限 {attribute.MinimumLength} が上限 {attribute.MaximumLength} を超えています。");
                return null;
            }

            return DeclaredValidators.Create(typeof(StringLengthRecordValidator<>), recordType, member, primaryKey, attribute);
        }
    }
}
#endif
