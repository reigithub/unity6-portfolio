#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Game.Shared.Scriptable.Database.Validation
{
    /// <summary>
    /// ScriptableDatabase の構成（テーブル結線・生成漏れ・外部キー宣言）を検査した結果。
    /// データの中身を見る前段として、検証が「対象を取りこぼしたまま成功する」状態を潰すために走る。
    /// </summary>
    internal sealed class ScriptableDatabaseSchema
    {
        /// <summary>レコード型 → 結線済みテーブル。</summary>
        public IReadOnlyDictionary<Type, ScriptableTableBase> Tables { get; }

        /// <summary>宣言として妥当だった外部キー。</summary>
        public IReadOnlyList<ForeignKeyDeclarations.Declaration> ForeignKeys { get; }

        public ValidationResult Result { get; }

        private ScriptableDatabaseSchema(
            IReadOnlyDictionary<Type, ScriptableTableBase> tables,
            IReadOnlyList<ForeignKeyDeclarations.Declaration> foreignKeys,
            ValidationResult result)
        {
            Tables = tables;
            ForeignKeys = foreignKeys;
            Result = result;
        }

        public static ScriptableDatabaseSchema Inspect(ScriptableObject database)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));

            var tables = new Dictionary<Type, ScriptableTableBase>();
            var declaredRecordTypes = new HashSet<Type>();
            var result = new ValidationResult(ValidationExecutor.ConfigurationResultName, 0);

            CollectTables(database, tables, declaredRecordTypes, result);
            DetectMissingTableFields(declaredRecordTypes, result);

            var foreignKeys = ForeignKeyDeclarations.Collect(declaredRecordTypes, new HashSet<Type>(tables.Keys), result);

            return new ScriptableDatabaseSchema(tables, foreignKeys, result);
        }

        /// <summary>結線済みテーブルのレコード型だけを、型走査なしで列挙する（一覧表示用）。</summary>
        public static List<Type> WiredRecordTypes(ScriptableObject database)
        {
            var recordTypes = new List<Type>();
            if (database == null) return recordTypes;

            foreach (var (_, recordType, table) in TableFields(database))
            {
                if (table != null && !recordTypes.Contains(recordType)) recordTypes.Add(recordType);
            }

            return recordTypes;
        }

        /// <summary>
        /// ScriptableDatabase が持つテーブルフィールドを走査する（生成型へコンパイル時依存しない）。
        /// レコード型を解決できないフィールドは返さない。
        /// </summary>
        private static IEnumerable<(FieldInfo Field, Type RecordType, ScriptableTableBase Table)> TableFields(ScriptableObject database)
        {
            var fields = database.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (var field in fields)
            {
                if (!typeof(ScriptableTableBase).IsAssignableFrom(field.FieldType)) continue;
                if (!ValidationReflection.TryGetRecordType(field.FieldType, out var recordType)) continue;

                // 破棄済み参照も null 扱いにするため、Unity の等値演算子を通す。
                var table = field.GetValue(database) as ScriptableTableBase;
                yield return (field, recordType, table == null ? null : table);
            }
        }

        private static void CollectTables(
            ScriptableObject database,
            Dictionary<Type, ScriptableTableBase> tables,
            HashSet<Type> declaredRecordTypes,
            ValidationResult result)
        {
            foreach (var (field, recordType, table) in TableFields(database))
            {
                declaredRecordTypes.Add(recordType);

                if (table == null)
                {
                    result.AddError(recordType.Name,
                        $"テーブル資産が未結線です（フィールド '{field.Name}'）。ScriptableDatabaseWindow の Register を実行してください。");
                    continue;
                }

                if (tables.ContainsKey(recordType))
                {
                    result.AddError(recordType.Name, $"同じレコード型のテーブルフィールドが複数あります（'{field.Name}'）。");
                    continue;
                }

                tables.Add(recordType, table);
            }
        }

        // [ScriptableTable] があるのに ScriptableDatabase にフィールドが無い＝ Build 漏れ。
        private static void DetectMissingTableFields(HashSet<Type> declaredRecordTypes, ValidationResult result)
        {
            foreach (var recordType in ValidationReflection.RecordTypes())
            {
                if (declaredRecordTypes.Contains(recordType)) continue;

                result.AddError(recordType.Name,
                    "ScriptableDatabase にテーブルフィールドがありません。ScriptableDatabaseWindow の Build を実行してください。");
            }
        }
    }
}
#endif
