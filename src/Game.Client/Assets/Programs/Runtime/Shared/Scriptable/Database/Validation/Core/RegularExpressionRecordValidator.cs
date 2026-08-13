#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Game.Shared.Scriptable.Database.Validation
{
    /// <summary>
    /// <see cref="RegularExpressionAttribute"/> の宣言から作られる書式チェック。
    /// null は検証しない（<see cref="StringNotNullAttribute"/> の担当）。
    /// </summary>
    internal sealed class RegularExpressionRecordValidator<TRecord> : IRecordValidator<TRecord>
    {
        private readonly MemberInfo _member;
        private readonly MemberInfo _primaryKeyMember;
        private readonly Regex _regex;

        public RegularExpressionRecordValidator(MemberInfo member, MemberInfo primaryKeyMember, Regex regex)
        {
            _member = member;
            _primaryKeyMember = primaryKeyMember;
            _regex = regex;
        }

        public void Validate(TRecord record, ValidationResult result, IRecordGetter recordGetter)
        {
            var value = (string)ValidationReflection.GetValue(_member, record);
            if (value == null) return;
            if (_regex.IsMatch(value)) return;

            result.AddError(
                ValidationReflection.GetValue(_primaryKeyMember, record).ToString(),
                $"{_member.Name}=\"{value}\" が書式 {_regex} に一致しません。");
        }
    }

    internal static class RegularExpressionRecordValidator
    {
        public static object Create(
            Type recordType, MemberInfo member, MemberInfo primaryKey, RegularExpressionAttribute attribute, ValidationResult result)
        {
            if (!DeclaredValidators.RequireMemberType(recordType, member, "[RegularExpression]", result, typeof(string))) return null;

            string where = DeclaredValidators.Describe(recordType, member);

            if (string.IsNullOrEmpty(attribute.Pattern))
            {
                result.AddError(recordType.Name, $"{where} の [RegularExpression] にパターンが指定されていません。");
                return null;
            }

            // パターンの検査を兼ねてここで 1 回だけ組み立て、レコードごとの再解析を避ける。
            Regex regex;
            try
            {
                regex = new Regex(attribute.Pattern);
            }
            catch (ArgumentException e)
            {
                result.AddError(recordType.Name, $"{where} の [RegularExpression] のパターンが不正です: {e.Message}");
                return null;
            }

            return DeclaredValidators.Create(typeof(RegularExpressionRecordValidator<>), recordType, member, primaryKey, regex);
        }
    }
}
#endif
