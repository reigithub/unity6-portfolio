#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Game.Shared.Scriptable.Database.Validation
{
    /// <summary>検証機構が使うスキーマ走査ヘルパ。</summary>
    internal static class ValidationReflection
    {
        /// <summary>ロード済み全アセンブリの型。型ロードに失敗したアセンブリは読める型だけを返す。</summary>
        public static IEnumerable<Type> AllTypes() =>
            AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeTypes);

        private static IEnumerable<Type> SafeTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
        }

        /// <summary><see cref="ScriptableTableAttribute"/> が付いたレコード型。</summary>
        public static IEnumerable<Type> RecordTypes() =>
            AllTypes().Where(t => t.IsClass && t.GetCustomAttribute<ScriptableTableAttribute>() != null);

        /// <summary>基底をたどって指定の開いたジェネリック型を探し、その型引数を返す。</summary>
        public static bool TryGetGenericBaseArguments(Type type, Type openGeneric, out Type[] arguments)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                if (t.IsGenericType && t.GetGenericTypeDefinition() == openGeneric)
                {
                    arguments = t.GetGenericArguments();
                    return true;
                }
            }

            arguments = null;
            return false;
        }

        /// <summary>テーブル型が保持するレコード型（<c>ScriptableTable&lt;TRecord&gt;</c> の型引数）。</summary>
        public static bool TryGetRecordType(Type tableType, out Type recordType)
        {
            if (TryGetGenericBaseArguments(tableType, typeof(ScriptableTable<>), out var arguments))
            {
                recordType = arguments[0];
                return true;
            }

            recordType = null;
            return false;
        }

        /// <summary>
        /// int の主キーメンバ。<see cref="ForeignKeyAttribute"/> の照合も
        /// エラー箇所の特定も int 主キーを前提とするため、判定はここに集約する。
        /// </summary>
        public static bool TryFindIntPrimaryKey(Type recordType, out MemberInfo primaryKey)
        {
            primaryKey = Columns(recordType).FirstOrDefault(m => m.GetCustomAttribute<PrimaryKeyAttribute>() != null);
            return primaryKey != null && MemberType(primaryKey) == typeof(int);
        }

        /// <summary>外部キー宣言のあるメンバ。</summary>
        public static IEnumerable<(MemberInfo Member, ForeignKeyAttribute Attribute)> ForeignKeys(Type recordType) =>
            Columns(recordType)
                .Select(m => (Member: m, Attribute: m.GetCustomAttribute<ForeignKeyAttribute>()))
                .Where(x => x.Attribute != null);

        public static Type MemberType(MemberInfo member) =>
            member is FieldInfo f ? f.FieldType : ((PropertyInfo)member).PropertyType;

        public static bool IsReadable(MemberInfo member) =>
            member is FieldInfo || ((PropertyInfo)member).CanRead;

        public static object GetValue(MemberInfo member, object instance) =>
            member is FieldInfo f ? f.GetValue(instance) : ((PropertyInfo)member).GetValue(instance);

        // 列対象メンバ（public プロパティ / public フィールド）。宣言順。
        private static IEnumerable<MemberInfo> Columns(Type recordType) =>
            recordType
                .GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m is FieldInfo || (m is PropertyInfo p && p.GetIndexParameters().Length == 0))
                .OrderBy(m => m.MetadataToken);
    }
}
#endif
