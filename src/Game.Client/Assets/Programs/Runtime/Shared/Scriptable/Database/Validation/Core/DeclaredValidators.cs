#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Game.Shared.Scriptable.Database.Validation
{
    /// <summary>
    /// レコード型に付いた検証属性を、実行できる validator へ解決する単一入口。
    /// 型情報だけで完結する（資産にも ScriptableDatabase にも依存しない）。
    /// 宣言として成立しないものは黙って落とさず、構成エラーとして記録する。
    /// </summary>
    internal static class DeclaredValidators
    {
        // 主キー要件の判定に使う検証属性の一覧。属性を増やすときはここと CollectFrom の両方へ追加する。
        private static readonly Type[] _attributeTypes =
        {
            typeof(ForeignKeyAttribute),
            typeof(StringNotNullAttribute),
            typeof(ValueRangeAttribute),
            typeof(StringLengthAttribute),
            typeof(RegularExpressionAttribute),
            typeof(CompareAttribute),
            typeof(UniqueAttribute),
        };

        /// <summary>宣言 1 件から validator を作る。宣言が不正なら <paramref name="result"/> へ記録して null を返す。</summary>
        private delegate object ValidatorFactory<TAttribute>(
            Type recordType, MemberInfo member, MemberInfo primaryKey, TAttribute attribute, ValidationResult result);

        /// <param name="availableRecordTypes">外部キーの参照先として解決できるレコード型。</param>
        /// <returns>レコード単位・テーブル横断の両方を含む validator。振り分けは登録側が型で行う。</returns>
        public static IReadOnlyList<object> Collect(
            IEnumerable<Type> recordTypes, HashSet<Type> availableRecordTypes, ValidationResult result)
        {
            var validators = new List<object>();
            foreach (var recordType in recordTypes)
            {
                CollectFrom(recordType, availableRecordTypes, result, validators);
            }

            return validators;
        }

        private static void CollectFrom(
            Type recordType, HashSet<Type> availableRecordTypes, ValidationResult result, List<object> validators)
        {
            // エラー箇所の特定にレコードの主キー値を使うため、検証属性を持つ型には int 主キーを必須にする。
            if (!ValidationReflection.TryFindIntPrimaryKey(recordType, out var primaryKey))
            {
                if (HasAnyDeclaration(recordType))
                {
                    result.AddError(recordType.Name,
                        "検証属性を使うには int の [PrimaryKey] が必要です（エラー箇所の特定に使います）。");
                }

                return;
            }

            CollectDeclarations<ForeignKeyAttribute>(recordType, primaryKey, result, validators,
                (t, m, pk, a, r) => ForeignKeyRecordValidator.Create(t, m, pk, a, availableRecordTypes, r));
            CollectDeclarations<StringNotNullAttribute>(recordType, primaryKey, result, validators, StringNotNullRecordValidator.Create);
            CollectDeclarations<ValueRangeAttribute>(recordType, primaryKey, result, validators, ValueRangeRecordValidator.Create);
            CollectDeclarations<StringLengthAttribute>(recordType, primaryKey, result, validators, StringLengthRecordValidator.Create);
            CollectDeclarations<RegularExpressionAttribute>(recordType, primaryKey, result, validators, RegularExpressionRecordValidator.Create);
            CollectDeclarations<CompareAttribute>(recordType, primaryKey, result, validators, CompareRecordValidator.Create);
            CollectDeclarations<UniqueAttribute>(recordType, primaryKey, result, validators, UniqueTableRecordsValidator.Create);
        }

        private static void CollectDeclarations<TAttribute>(
            Type recordType, MemberInfo primaryKey, ValidationResult result, List<object> validators,
            ValidatorFactory<TAttribute> factory)
            where TAttribute : Attribute
        {
            foreach (var (member, attribute) in ValidationReflection.Declarations<TAttribute>(recordType))
            {
                if (!ValidationReflection.IsReadable(member))
                {
                    result.AddError(recordType.Name, $"{Describe(recordType, member)} は読み取れないため検証属性を付けられません。");
                    continue;
                }

                var validator = factory(recordType, member, primaryKey, attribute, result);
                if (validator != null) validators.Add(validator);
            }
        }

        private static bool HasAnyDeclaration(Type recordType) =>
            ValidationReflection.Columns(recordType)
                .Any(member => _attributeTypes.Any(attributeType => member.IsDefined(attributeType, inherit: true)));

        // ---- 各 validator の宣言検査から使う共通処理 ----

        /// <summary>宣言先のメンバ型を確認する。許容外なら構成エラーを記録して false を返す。</summary>
        public static bool RequireMemberType(
            Type recordType, MemberInfo member, string attributeName, ValidationResult result, params Type[] allowed)
        {
            if (Array.IndexOf(allowed, ValidationReflection.MemberType(member)) >= 0) return true;

            result.AddError(recordType.Name,
                $"{Describe(recordType, member)} は {string.Join(" / ", allowed.Select(t => t.Name))} ではないため {attributeName} を付けられません。");
            return false;
        }

        /// <summary>レコード型が実行時にしか決まらない発見経路から validator を生成する。</summary>
        public static object Create(Type openValidatorType, Type recordType, params object[] arguments) =>
            Activator.CreateInstance(openValidatorType.MakeGenericType(recordType), arguments);

        /// <summary>構成エラーで宣言箇所を示す表記。</summary>
        public static string Describe(Type recordType, MemberInfo member) => $"{recordType.Name}.{member.Name}";
    }
}
#endif
