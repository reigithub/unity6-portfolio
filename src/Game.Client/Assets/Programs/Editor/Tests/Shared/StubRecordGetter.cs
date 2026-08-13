using System;
using System.Collections.Generic;
using System.Linq;
using Game.Shared.Scriptable.Database.Validation;

namespace Game.Tests.Shared
{
    /// <summary>
    /// 検証エンジンのテスト用レコード供給。ScriptableDatabase 資産を介さずレコードを直接与える。
    /// 主キーは Id プロパティ固定（テスト用レコードの取り決め）。
    /// </summary>
    internal sealed class StubRecordGetter : IRecordGetter
    {
        private readonly Dictionary<Type, List<object>> _records = new();

        public void Add<TRecord>(params TRecord[] records) =>
            _records[typeof(TRecord)] = records.Cast<object>().ToList();

        public IReadOnlyList<TRecord> GetAll<TRecord>() => Records(typeof(TRecord)).Cast<TRecord>().ToList();

        public bool ContainsPrimaryKey(Type targetRecordType, int primaryKey)
        {
            var property = targetRecordType.GetProperty("Id");
            return Records(targetRecordType).Any(r => (int)property.GetValue(r) == primaryKey);
        }

        private List<object> Records(Type recordType)
        {
            if (_records.TryGetValue(recordType, out var records)) return records;

            throw new InvalidOperationException($"{recordType.Name} は登録されていません。");
        }
    }
}
