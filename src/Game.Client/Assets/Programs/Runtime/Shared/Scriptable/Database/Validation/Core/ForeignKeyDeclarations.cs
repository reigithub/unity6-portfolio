#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Game.Shared.Scriptable.Database.Validation
{
    /// <summary>
    /// レコード型に付いた <see cref="ForeignKeyAttribute"/> を、検証に使える形へ解決する。
    /// 型情報だけで完結する（資産にも ScriptableDatabase にも依存しない）。
    /// </summary>
    internal static class ForeignKeyDeclarations
    {
        internal readonly struct Declaration
        {
            public Type RecordType { get; }
            public MemberInfo Member { get; }
            public MemberInfo PrimaryKeyMember { get; }
            public ForeignKeyAttribute Attribute { get; }

            public Declaration(Type recordType, MemberInfo member, MemberInfo primaryKeyMember, ForeignKeyAttribute attribute)
            {
                RecordType = recordType;
                Member = member;
                PrimaryKeyMember = primaryKeyMember;
                Attribute = attribute;
            }
        }

        /// <summary>
        /// 宣言を集める。宣言として成立しないものは黙って落とさず <paramref name="result"/> へエラーとして記録する。
        /// </summary>
        /// <param name="availableRecordTypes">参照先として解決できるレコード型。</param>
        public static List<Declaration> Collect(
            IEnumerable<Type> recordTypes,
            HashSet<Type> availableRecordTypes,
            ValidationResult result)
        {
            var declarations = new List<Declaration>();

            foreach (var recordType in recordTypes)
            {
                var foreignKeys = ValidationReflection.ForeignKeys(recordType).ToList();
                if (foreignKeys.Count == 0) continue;

                if (!ValidationReflection.TryFindIntPrimaryKey(recordType, out var primaryKey))
                {
                    result.AddError(recordType.Name, "[ForeignKey] を使うには int の [PrimaryKey] が必要です（エラー箇所の特定に使います）。");
                    continue;
                }

                foreach (var (member, attribute) in foreignKeys)
                {
                    if (IsInvalid(recordType, member, attribute, availableRecordTypes, result)) continue;

                    declarations.Add(new Declaration(recordType, member, primaryKey, attribute));
                }
            }

            return declarations;
        }

        private static bool IsInvalid(
            Type recordType,
            MemberInfo member,
            ForeignKeyAttribute attribute,
            HashSet<Type> availableRecordTypes,
            ValidationResult result)
        {
            string where = $"{recordType.Name}.{member.Name}";

            if (ValidationReflection.MemberType(member) != typeof(int))
            {
                result.AddError(recordType.Name, $"{where} は int ではないため [ForeignKey] を付けられません。");
                return true;
            }

            if (!ValidationReflection.IsReadable(member))
            {
                result.AddError(recordType.Name, $"{where} は読み取れないため [ForeignKey] を付けられません。");
                return true;
            }

            var target = attribute.TargetRecordType;
            if (target == null)
            {
                result.AddError(recordType.Name, $"{where} の [ForeignKey] に参照先の型が指定されていません。");
                return true;
            }

            if (!ValidationReflection.TryFindIntPrimaryKey(target, out _))
            {
                result.AddError(recordType.Name, $"{where} の参照先 {target.Name} に int の [PrimaryKey] がありません。");
                return true;
            }

            if (!availableRecordTypes.Contains(target))
            {
                result.AddError(recordType.Name, $"{where} の参照先 {target.Name} のテーブルが検証対象にありません。");
                return true;
            }

            return false;
        }
    }
}
#endif
