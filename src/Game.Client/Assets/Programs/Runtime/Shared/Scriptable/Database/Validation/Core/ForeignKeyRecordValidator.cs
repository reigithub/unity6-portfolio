#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Game.Shared.Scriptable.Database.Validation
{
    /// <summary>
    /// <see cref="ForeignKeyAttribute"/> の宣言から作られる存在チェック。
    /// 参照先の主キー集合に対する O(1) 照合のみを行う（全走査はしない）。
    /// </summary>
    internal sealed class ForeignKeyRecordValidator<TRecord> : IRecordValidator<TRecord>
    {
        private readonly MemberInfo _member;
        private readonly MemberInfo _primaryKeyMember;
        private readonly Type _targetRecordType;
        private readonly bool _allowNone;

        public ForeignKeyRecordValidator(MemberInfo member, MemberInfo primaryKeyMember, ForeignKeyAttribute attribute)
        {
            _member = member;
            _primaryKeyMember = primaryKeyMember;
            _targetRecordType = attribute.TargetRecordType;
            _allowNone = attribute.AllowNone;
        }

        public void Validate(TRecord record, ValidationResult result, IRecordGetter recordGetter)
        {
            int value = (int)ValidationReflection.GetValue(_member, record);
            if (value == 0 && _allowNone) return;
            if (recordGetter.ContainsPrimaryKey(_targetRecordType, value)) return;

            result.AddError(
                ValidationReflection.GetValue(_primaryKeyMember, record).ToString(),
                $"{_member.Name}={value} に対応する {_targetRecordType.Name} のレコードがありません。");
        }
    }

    internal static class ForeignKeyRecordValidator
    {
        /// <param name="availableRecordTypes">参照先として解決できるレコード型。</param>
        public static object Create(
            Type recordType, MemberInfo member, MemberInfo primaryKey, ForeignKeyAttribute attribute,
            HashSet<Type> availableRecordTypes, ValidationResult result)
        {
            if (!DeclaredValidators.RequireMemberType(recordType, member, "[ForeignKey]", result, typeof(int))) return null;

            string where = DeclaredValidators.Describe(recordType, member);
            var target = attribute.TargetRecordType;

            if (target == null)
            {
                result.AddError(recordType.Name, $"{where} の [ForeignKey] に参照先の型が指定されていません。");
                return null;
            }

            if (!ValidationReflection.TryFindIntPrimaryKey(target, out _))
            {
                result.AddError(recordType.Name, $"{where} の参照先 {target.Name} に int の [PrimaryKey] がありません。");
                return null;
            }

            if (!availableRecordTypes.Contains(target))
            {
                result.AddError(recordType.Name, $"{where} の参照先 {target.Name} のテーブルが検証対象にありません。");
                return null;
            }

            return DeclaredValidators.Create(typeof(ForeignKeyRecordValidator<>), recordType, member, primaryKey, attribute);
        }
    }
}
#endif
