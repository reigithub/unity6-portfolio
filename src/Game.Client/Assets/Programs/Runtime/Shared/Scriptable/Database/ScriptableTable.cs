using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Shared.Scriptable.Database
{
    /// <summary>
    /// ScriptableObject ベースのテーブル基底。レコードを主キー昇順のソート済み配列で保持する。
    /// 全件列挙・LINQ は <see cref="All"/> が返すビュー（<see cref="ScriptableTableRecords{T}"/>）上で行う。
    /// 主キー（<c>FindById</c> 等）・二次/複合キーの型付きファインダ、および編集時の
    /// 主キー整列（OnValidate）は、いずれも生成 partial が本クラスの共通コアを呼んで実装する。
    /// 本基底はキー型・レコード型（参照/値）に一切依存しない。
    /// not-found 表現は MasterMemory に準拠：<see cref="FindUnique"/> は既定で例外、
    /// <see cref="TryFindUnique"/> は <c>default</c>＋false、<see cref="FindClosest"/> は空時に <c>default</c>。
    /// </summary>
    public abstract class ScriptableTable<TRecord> : ScriptableObject
    {
        [SerializeField] protected TRecord[] records = Array.Empty<TRecord>();

        public ScriptableTableRecords<TRecord> All => new(records, 0, records.Length - 1, true);
        public ScriptableTableRecords<TRecord> AllReverse => new(records, 0, records.Length - 1, false);

        protected static TRecord[] BuildSortedIndex<TKey>(TRecord[] src, Func<TRecord, TKey> sel, IComparer<TKey> cmp)
        {
            var items = new TRecord[src.Length];
            var keys = new TKey[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                items[i] = src[i];
                keys[i] = sel(src[i]);
            }
            Array.Sort(keys, items, 0, items.Length, cmp);
            return items;
        }

        /// <summary>一意キーで検索。見つからなければ既定で例外。throwIfNotFound=false なら default。</summary>
        protected static TRecord FindUnique<TKey>(TRecord[] idx, TKey key, Func<TRecord, TKey> sel, IComparer<TKey> cmp, bool throwIfNotFound = true)
        {
            int i = BinarySearch.FindFirst(idx, key, sel, cmp);
            if (i >= 0) return idx[i];
            if (throwIfNotFound) ThrowKeyNotFound(key);
            return default;
        }

        protected static bool TryFindUnique<TKey>(TRecord[] idx, TKey key, Func<TRecord, TKey> sel, IComparer<TKey> cmp, out TRecord record)
        {
            int i = BinarySearch.FindFirst(idx, key, sel, cmp);
            if (i >= 0) { record = idx[i]; return true; }
            record = default;
            return false;
        }

        protected static ScriptableTableRecords<TRecord> FindMany<TKey>(TRecord[] idx, Func<TRecord, TKey> sel, IComparer<TKey> cmp, TKey key)
        {
            int lo = BinarySearch.LowerBound(idx, key, sel, cmp);
            int hi = BinarySearch.UpperBound(idx, key, sel, cmp) - 1;
            return new ScriptableTableRecords<TRecord>(idx, lo, hi, true);
        }

        /// <summary>
        /// ユニークキーの近傍 1 件
        /// floor/ceiling 意味論：該当側に要素が無ければ default。
        /// </summary>
        protected static TRecord FindClosest<TKey>(TRecord[] idx, TKey key, Func<TRecord, TKey> sel, IComparer<TKey> cmp, bool selectLower)
        {
            int i = BinarySearch.FindClosest(idx, key, sel, cmp, selectLower);
            return (i >= 0 && i < idx.Length) ? idx[i] : default;
        }

        /// <summary>
        /// 非ユニークキーの近傍：最近傍キー値を求め、そのキーを持つ全件を返す
        /// </summary>
        protected static ScriptableTableRecords<TRecord> FindManyClosest<TKey>(TRecord[] idx, TKey key, Func<TRecord, TKey> sel, IComparer<TKey> cmp, bool selectLower)
        {
            int i = BinarySearch.FindClosest(idx, key, sel, cmp, selectLower);
            if (i < 0 || i >= idx.Length) return default;
            return FindMany(idx, sel, cmp, sel(idx[i]));
        }

        protected static ScriptableTableRecords<TRecord> FindRange<TKey>(TRecord[] idx, TKey min, TKey max, Func<TRecord, TKey> sel, IComparer<TKey> cmp, bool ascending)
        {
            int lo = BinarySearch.LowerBound(idx, min, sel, cmp);
            int hi = BinarySearch.UpperBound(idx, max, sel, cmp) - 1;
            return new ScriptableTableRecords<TRecord>(idx, lo, hi, ascending);
        }

        private static TRecord ThrowKeyNotFound<TKey>(TKey key) => throw new KeyNotFoundException($"DataType: {typeof(TRecord).FullName}, Key: {key}");

#if UNITY_EDITOR
        /// <summary>
        /// 生成 partial の OnValidate が主キーセレクタ付きで呼ぶ、編集時の整列＋重複警告コア。
        /// records を <paramref name="sel"/> 昇順へ整列し、重複キーを警告する。
        /// </summary>
        protected void SortAndValidate<TKey>(Func<TRecord, TKey> sel, IComparer<TKey> cmp)
        {
            if (records == null) return;

            // 編集中の空スロット(null)を非 null を前方へ詰めて取り除く（切り詰めるため All/索引に null が漏れない）。
            // 値型レコードでは null が無いため count==Length となり Resize は no-op。
            int count = 0;
            for (int i = 0; i < records.Length; i++)
                if (records[i] != null) records[count++] = records[i];
            if (count != records.Length) Array.Resize(ref records, count);

            // キーを一度だけ計算して key/value ソート（比較ごとの再評価を避ける）。
            var keys = new TKey[count];
            for (int i = 0; i < count; i++) keys[i] = sel(records[i]);
            Array.Sort(keys, records, 0, count, cmp);

            // ソート済みキー配列で隣接重複を検出（sel の再評価なし）。
            for (int i = 1; i < count; i++)
            {
                if (cmp.Compare(keys[i], keys[i - 1]) == 0)
                    Debug.LogWarning($"[{name}] 主キー {keys[i]} が重複しています。", this);
            }
        }
#endif
    }
}
