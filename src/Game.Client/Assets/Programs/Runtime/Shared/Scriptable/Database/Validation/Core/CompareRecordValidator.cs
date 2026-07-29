#if UNITY_EDITOR
using System;
using System.Reflection;

namespace Game.Shared.Scriptable.Database.Validation
{
    /// <summary>
    /// <see cref="CompareAttribute"/> の宣言から作られる、同一レコード内の 2 メンバの大小チェック。
    /// 対象は同じ値型どうしに限る（null の比較意味論が宣言から読み取れないため、参照型は宣言時に弾く）。
    /// </summary>
    internal sealed class CompareRecordValidator<TRecord> : IRecordValidator<TRecord>
    {
        private readonly MemberInfo _member;
        private readonly MemberInfo _otherMember;
        private readonly MemberInfo _primaryKeyMember;
        private readonly CompareOperator _operator;

        public CompareRecordValidator(
            MemberInfo member, MemberInfo otherMember, MemberInfo primaryKeyMember, CompareAttribute attribute)
        {
            _member = member;
            _otherMember = otherMember;
            _primaryKeyMember = primaryKeyMember;
            _operator = attribute.Operator;
        }

        public void Validate(TRecord record, ValidationResult result, IRecordGetter recordGetter)
        {
            var value = (IComparable)ValidationReflection.GetValue(_member, record);
            object other = ValidationReflection.GetValue(_otherMember, record);
            if (Satisfies(value.CompareTo(other))) return;

            result.AddError(
                ValidationReflection.GetValue(_primaryKeyMember, record).ToString(),
                $"{_member.Name}={value} は {_otherMember.Name}={other} {Describe(_operator)}なければなりません。");
        }

        private bool Satisfies(int comparison) => _operator switch
        {
            CompareOperator.Equal => comparison == 0,
            CompareOperator.NotEqual => comparison != 0,
            CompareOperator.LessThan => comparison < 0,
            CompareOperator.LessThanOrEqual => comparison <= 0,
            CompareOperator.GreaterThan => comparison > 0,
            CompareOperator.GreaterThanOrEqual => comparison >= 0,
            _ => throw new InvalidOperationException($"未対応の比較演算子です: {_operator}"),
        };

        private static string Describe(CompareOperator op) => op switch
        {
            CompareOperator.Equal => "と等しく",
            CompareOperator.NotEqual => "と異なら",
            CompareOperator.LessThan => "より小さく",
            CompareOperator.LessThanOrEqual => "以下で",
            CompareOperator.GreaterThan => "より大きく",
            CompareOperator.GreaterThanOrEqual => "以上で",
            _ => throw new InvalidOperationException($"未対応の比較演算子です: {op}"),
        };
    }

    internal static class CompareRecordValidator
    {
        public static object Create(
            Type recordType, MemberInfo member, MemberInfo primaryKey, CompareAttribute attribute, ValidationResult result)
        {
            string where = DeclaredValidators.Describe(recordType, member);

            if (!Enum.IsDefined(typeof(CompareOperator), attribute.Operator))
            {
                result.AddError(recordType.Name, $"{where} の [Compare] に未定義の比較演算子が指定されています。");
                return null;
            }

            if (string.IsNullOrEmpty(attribute.OtherMember))
            {
                result.AddError(recordType.Name, $"{where} の [Compare] に比較相手が指定されていません。");
                return null;
            }

            if (attribute.OtherMember == member.Name)
            {
                result.AddError(recordType.Name, $"{where} の [Compare] が自分自身を比較相手にしています。");
                return null;
            }

            var otherMember = ValidationReflection.FindColumn(recordType, attribute.OtherMember);
            if (otherMember == null)
            {
                result.AddError(recordType.Name, $"{where} の [Compare] の比較相手 {attribute.OtherMember} が見つかりません。");
                return null;
            }

            if (!ValidationReflection.IsReadable(otherMember))
            {
                result.AddError(recordType.Name, $"{where} の [Compare] の比較相手 {otherMember.Name} は読み取れません。");
                return null;
            }

            var memberType = ValidationReflection.MemberType(member);
            if (memberType != ValidationReflection.MemberType(otherMember))
            {
                result.AddError(recordType.Name,
                    $"{where} の [Compare] は型が異なるメンバ同士です（{memberType.Name} と {ValidationReflection.MemberType(otherMember).Name}）。");
                return null;
            }

            if (!memberType.IsValueType || !typeof(IComparable).IsAssignableFrom(memberType))
            {
                result.AddError(recordType.Name, $"{where} は比較可能な値型ではないため [Compare] を付けられません。");
                return null;
            }

            return DeclaredValidators.Create(
                typeof(CompareRecordValidator<>), recordType, member, otherMember, primaryKey, attribute);
        }
    }
}
#endif
