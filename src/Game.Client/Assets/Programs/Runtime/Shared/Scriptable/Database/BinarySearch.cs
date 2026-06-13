using System;
using System.Collections.Generic;

namespace Game.Shared.Scriptable.Database
{
    /// <summary>ソート済み配列への二分探索群。FindById・範囲・近傍はこれらで構成する。</summary>
    internal static class BinarySearch
    {
        /// <summary>key と一致する最初の要素の index。見つからなければ -1。</summary>
        public static int FindFirst<T, TKey>(T[] a, TKey key, Func<T, TKey> sel, IComparer<TKey> cmp)
        {
            int lo = 0, hi = a.Length - 1;
            while (lo <= hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                int c = cmp.Compare(sel(a[mid]), key);
                if (c == 0) return mid;
                if (c < 0) lo = mid + 1; else hi = mid - 1;
            }
            return -1;
        }

        /// <summary>sel(a[i]) &gt;= key となる最小 index（無ければ a.Length）。</summary>
        public static int LowerBound<T, TKey>(T[] a, TKey key, Func<T, TKey> sel, IComparer<TKey> cmp)
        {
            int lo = 0, hi = a.Length;
            while (lo < hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                if (cmp.Compare(sel(a[mid]), key) < 0) lo = mid + 1; else hi = mid;
            }
            return lo;
        }

        /// <summary>sel(a[i]) &gt; key となる最小 index（無ければ a.Length）。</summary>
        public static int UpperBound<T, TKey>(T[] a, TKey key, Func<T, TKey> sel, IComparer<TKey> cmp)
        {
            int lo = 0, hi = a.Length;
            while (lo < hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                if (cmp.Compare(sel(a[mid]), key) <= 0) lo = mid + 1; else hi = mid;
            }
            return lo;
        }

        /// <summary>
        /// 近傍要素の index を返す（MasterMemory.BinarySearch.FindClosest 準拠）。
        /// 完全一致があればその index。無ければ selectLower=true で key 未満の最大要素の index（無ければ -1）、
        /// selectLower=false で key 超の最小要素の index（無ければ a.Length）。空配列は -1。
        /// </summary>
        public static int FindClosest<T, TKey>(T[] a, TKey key, Func<T, TKey> sel, IComparer<TKey> cmp, bool selectLower)
        {
            if (a.Length == 0) return -1;
            int lo = -1, hi = a.Length;
            while (hi - lo > 1)
            {
                int mid = lo + ((hi - lo) >> 1);
                int found = cmp.Compare(sel(a[mid]), key);
                if (found == 0) { lo = hi = mid; break; }
                if (found >= 1) hi = mid; else lo = mid;
            }
            return selectLower ? lo : hi;
        }
    }
}
