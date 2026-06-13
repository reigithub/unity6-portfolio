using System;
using System.Collections;
using System.Collections.Generic;

namespace Game.Shared.Scriptable.Database
{
    /// <summary>
    /// ソート済み配列の連続区間を指すゼロアロケーションなビュー。
    /// struct 列挙子を持つため foreach は追加アロケーションなしで列挙でき、
    /// <see cref="IEnumerable{T}"/> 実装により LINQ もこのビュー上で行える。
    /// </summary>
    public readonly struct ScriptableTableRecords<T> : IReadOnlyList<T>
    {
        private readonly T[] _records;
        private readonly int _left;
        private readonly int _right;
        private readonly bool _ascending;

        public ScriptableTableRecords(T[] records, int left, int right, bool ascending)
        {
            bool ok = records != null && left <= right && left >= 0 && right < records.Length;
            _records = ok ? records : null;
            _left = ok ? left : 0;
            _right = ok ? right : -1;
            _ascending = ascending;
        }

        public int Count => _records == null ? 0 : _right - _left + 1;

        public bool IsEmpty => Count == 0;

        public T this[int index]
        {
            get
            {
                if (index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
                return _ascending ? _records[_left + index] : _records[_right - index];
            }
        }

        /// <summary>foreach 用のゼロアロケーション列挙子（struct）。</summary>
        public Enumerator GetEnumerator() => new Enumerator(this);

        // LINQ 等が辿る interface 経路。boxing する代わりにビュー本来の用途（foreach）は上の struct 版が処理する。
        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            for (int i = 0; i < Count; i++) yield return this[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)this).GetEnumerator();

        public struct Enumerator
        {
            private readonly ScriptableTableRecords<T> _range;
            private int _index;

            public Enumerator(ScriptableTableRecords<T> range)
            {
                _range = range;
                _index = -1;
            }

            public bool MoveNext() => ++_index < _range.Count;

            public T Current => _range[_index];
        }
    }
}
