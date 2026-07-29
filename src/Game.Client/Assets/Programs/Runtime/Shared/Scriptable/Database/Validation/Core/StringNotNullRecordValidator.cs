#if UNITY_EDITOR
using System;
using System.Reflection;

namespace Game.Shared.Scriptable.Database.Validation
{
    /// <summary><see cref="StringNotNullAttribute"/> の宣言から作られる未設定チェック。</summary>
    internal sealed class StringNotNullRecordValidator<TRecord> : IRecordValidator<TRecord>
    {
        private readonly MemberInfo _member;
        private readonly MemberInfo _primaryKeyMember;
        private readonly bool _allowEmpty;

        public StringNotNullRecordValidator(MemberInfo member, MemberInfo primaryKeyMember, StringNotNullAttribute attribute)
        {
            _member = member;
            _primaryKeyMember = primaryKeyMember;
            _allowEmpty = attribute.AllowEmpty;
        }

        public void Validate(TRecord record, ValidationResult result, IRecordGetter recordGetter)
        {
            var value = (string)ValidationReflection.GetValue(_member, record);
            if (value != null && (_allowEmpty || value.Length > 0)) return;

            result.AddError(
                ValidationReflection.GetValue(_primaryKeyMember, record).ToString(),
                $"{_member.Name} が{(value == null ? "未設定" : "空")}です。");
        }
    }

    internal static class StringNotNullRecordValidator
    {
        public static object Create(
            Type recordType, MemberInfo member, MemberInfo primaryKey, StringNotNullAttribute attribute, ValidationResult result)
        {
            if (!DeclaredValidators.RequireMemberType(recordType, member, "[StringNotNull]", result, typeof(string))) return null;

            return DeclaredValidators.Create(typeof(StringNotNullRecordValidator<>), recordType, member, primaryKey, attribute);
        }
    }
}
#endif
