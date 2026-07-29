#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Game.Shared.Scriptable.Database.Validation
{
    /// <summary>
    /// <see cref="UniqueAttribute"/> の宣言から作られる重複チェック。
    /// null は「未設定」として重複判定の対象にしない。
    /// </summary>
    internal sealed class UniqueRecordsValidator<TRecord> : IRecordsValidator<TRecord>
    {
        private readonly MemberInfo _member;
        private readonly MemberInfo _primaryKeyMember;

        public UniqueRecordsValidator(MemberInfo member, MemberInfo primaryKeyMember)
        {
            _member = member;
            _primaryKeyMember = primaryKeyMember;
        }

        public void Validate(IReadOnlyList<TRecord> allRecords, ValidationResult result, IRecordGetter recordGetter)
        {
            // 値 → 最初に現れたレコードの主キー。2 件目以降を重複として報告する。
            var firstKeys = new Dictionary<object, string>();

            for (int i = 0; i < allRecords.Count; i++)
            {
                var record = allRecords[i];
                if (record is null) continue;

                object value = ValidationReflection.GetValue(_member, record);
                if (value == null) continue;

                string key = ValidationReflection.GetValue(_primaryKeyMember, record).ToString();
                if (firstKeys.TryGetValue(value, out var firstKey))
                {
                    result.AddError(key, $"{_member.Name}={value} が主キー {firstKey} のレコードと重複しています。");
                    continue;
                }

                firstKeys.Add(value, key);
            }
        }
    }

    internal static class UniqueTableRecordsValidator
    {
        public static object Create(
            Type recordType, MemberInfo member, MemberInfo primaryKey, UniqueAttribute attribute, ValidationResult result) =>
            DeclaredValidators.Create(typeof(UniqueRecordsValidator<>), recordType, member, primaryKey);
    }
}
#endif
