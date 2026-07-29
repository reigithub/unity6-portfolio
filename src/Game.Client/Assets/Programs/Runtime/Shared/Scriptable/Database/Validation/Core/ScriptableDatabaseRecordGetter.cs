#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;

namespace Game.Shared.Scriptable.Database.Validation
{
    /// <summary>
    /// ScriptableDatabase の結線済みテーブルからレコードを供給する <see cref="IRecordGetter"/>。
    /// 解決できないテーブル・主キーは 0 件として扱わず例外にする（検証を黙って通さないため）。
    /// </summary>
    public sealed class ScriptableDatabaseRecordGetter : IRecordGetter
    {
        private readonly IReadOnlyDictionary<Type, ScriptableTableBase> _tables;
        private readonly Dictionary<Type, HashSet<int>> _primaryKeySets = new();

        public ScriptableDatabaseRecordGetter(IReadOnlyDictionary<Type, ScriptableTableBase> tables)
        {
            _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        }

        public IReadOnlyList<TRecord> GetAll<TRecord>() => ((ScriptableTable<TRecord>)ResolveTable(typeof(TRecord))).All;

        public bool ContainsPrimaryKey(Type targetRecordType, int primaryKey)
        {
            if (targetRecordType == null) throw new ArgumentNullException(nameof(targetRecordType));

            if (!_primaryKeySets.TryGetValue(targetRecordType, out var keys))
            {
                keys = BuildPrimaryKeySet(targetRecordType);
                _primaryKeySets.Add(targetRecordType, keys);
            }

            return keys.Contains(primaryKey);
        }

        private ScriptableTableBase ResolveTable(Type recordType)
        {
            if (_tables.TryGetValue(recordType, out var table) && table != null) return table;

            throw new InvalidOperationException(
                $"{recordType.Name} のテーブルを ScriptableDatabase から解決できません。" +
                "ScriptableDatabaseWindow の Build / Register を実行してください。");
        }

        private HashSet<int> BuildPrimaryKeySet(Type recordType)
        {
            var table = ResolveTable(recordType);

            if (!ValidationReflection.TryFindIntPrimaryKey(recordType, out var primaryKey))
                throw new InvalidOperationException($"{recordType.Name} に int の [PrimaryKey] がありません。");

            // All はレコード型ごとに閉じたビューを返すため、非ジェネリック列挙で受ける。
            var records = (IEnumerable)table.GetType().GetProperty(nameof(ScriptableTable<object>.All)).GetValue(table);

            var keys = new HashSet<int>();
            foreach (var record in records)
            {
                keys.Add((int)ValidationReflection.GetValue(primaryKey, record));
            }

            return keys;
        }
    }
}
#endif
