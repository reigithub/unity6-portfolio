#if UNITY_EDITOR
using System;
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
        /// <summary>レコード型が実行時にしか決まらない発見経路から生成する。</summary>
        public static object Create(Type recordType, MemberInfo member, MemberInfo primaryKeyMember, ForeignKeyAttribute attribute)
        {
            var validatorType = typeof(ForeignKeyRecordValidator<>).MakeGenericType(recordType);
            return Activator.CreateInstance(validatorType, member, primaryKeyMember, attribute);
        }
    }
}
#endif
