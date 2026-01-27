using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Shared.Extensions
{
    /// <summary>
    /// ソートタイプ
    /// </summary>
    public enum SortType
    {
        /// <summary>ソートなし</summary>
        None,
        /// <summary>攻撃力順</summary>
        Attack,
        /// <summary>防御力順</summary>
        Defense,
    }

    /// <summary>
    /// ソート順序
    /// </summary>
    public enum OrderType
    {
        /// <summary>順序なし</summary>
        None,
        /// <summary>昇順</summary>
        Ascending,
        /// <summary>降順</summary>
        Descending
    }

    /// <summary>
    /// フィルタータイプ
    /// </summary>
    public enum FilterType
    {
        /// <summary>フィルターなし</summary>
        None,
        /// <summary>言語でフィルター</summary>
        Language,
        /// <summary>属性でフィルター</summary>
        Elements,
        /// <summary>複合属性でフィルター</summary>
        DoubleElements,
    }

    /// <summary>
    /// コレクションのソート用拡張メソッド
    /// </summary>
    public static class SortExtensions
    {
        /// <summary>
        /// コレクションを指定した順序でソートする
        /// </summary>
        /// <typeparam name="TItem">アイテムの型</typeparam>
        /// <typeparam name="TValue">ソートキーの型</typeparam>
        /// <param name="items">ソート対象のコレクション</param>
        /// <param name="orderType">ソート順序</param>
        /// <param name="predicate">ソートキーを取得する関数</param>
        /// <returns>ソートされたコレクション</returns>
        public static IEnumerable<TItem> Sorting<TItem, TValue>(this IEnumerable<TItem> items, OrderType orderType, Func<TItem, TValue> predicate)
        {
            switch (orderType)
            {
                case OrderType.Ascending:
                    return items.OrderBy(x => predicate(x));
                case OrderType.Descending:
                    return items.OrderByDescending(x => predicate(x));
                case OrderType.None:
                default:
                    return items;
            }
        }
    }

    /// <summary>
    /// コレクションのフィルタリング用拡張メソッド
    /// </summary>
    public static class FilterExtensions
    {
        /// <summary>
        /// フィルター条件のいずれかに一致するアイテムを抽出する（OR条件）
        /// </summary>
        /// <typeparam name="TItem">アイテムの型</typeparam>
        /// <param name="items">フィルター対象のコレクション</param>
        /// <param name="filterType">フィルタータイプ</param>
        /// <param name="filters">フィルター条件のディクショナリ</param>
        /// <param name="predicate">アイテムが条件に一致するか判定する関数</param>
        /// <returns>フィルタリングされたコレクション</returns>
        public static IEnumerable<TItem> Filtering<TItem>(this IEnumerable<TItem> items,
            FilterType filterType,
            IReadOnlyDictionary<FilterType, HashSet<int>> filters,
            Func<TItem, int, bool> predicate)
        {
            if (!filters.TryGetValue(filterType, out var values))
                return items;

            return items.Where(x => values.Any(y => predicate(x, y)));
        }

        /// <summary>
        /// フィルター条件のすべてに一致するアイテムを抽出する（AND条件）
        /// </summary>
        /// <typeparam name="TItem">アイテムの型</typeparam>
        /// <param name="items">フィルター対象のコレクション</param>
        /// <param name="filterType">フィルタータイプ</param>
        /// <param name="filters">フィルター条件のディクショナリ</param>
        /// <param name="predicate">アイテムが条件に一致するか判定する関数</param>
        /// <returns>フィルタリングされたコレクション</returns>
        public static IEnumerable<TItem> FilteringAll<TItem>(this IEnumerable<TItem> items,
            FilterType filterType,
            IReadOnlyDictionary<FilterType, HashSet<int>> filters,
            Func<TItem, int, bool> predicate)
        {
            if (!filters.TryGetValue(filterType, out var values))
                return items;

            return items.Where(x => values.All(y => predicate(x, y)));
        }

        /// <summary>
        /// 複数のフィルター条件を配列として受け取り、アイテムを抽出する
        /// </summary>
        /// <typeparam name="TItem">アイテムの型</typeparam>
        /// <param name="items">フィルター対象のコレクション</param>
        /// <param name="filterType">フィルタータイプ</param>
        /// <param name="filters">フィルター条件のディクショナリ</param>
        /// <param name="predicate">アイテムが条件に一致するか判定する関数（条件配列を受け取る）</param>
        /// <returns>フィルタリングされたコレクション</returns>
        public static IEnumerable<TItem> FilteringMultiple<TItem>(this IEnumerable<TItem> items,
            FilterType filterType,
            IReadOnlyDictionary<FilterType, HashSet<int>> filters,
            Func<TItem, int[], bool> predicate)
        {
            if (!filters.TryGetValue(filterType, out var values))
                return items;

            return items.Where(x => predicate(x, values.ToArray()));
        }

        /// <summary>
        /// 範囲条件でアイテムを抽出する
        /// </summary>
        /// <typeparam name="TItem">アイテムの型</typeparam>
        /// <param name="items">フィルター対象のコレクション</param>
        /// <param name="filterType">フィルタータイプ</param>
        /// <param name="filters">範囲フィルター条件のディクショナリ</param>
        /// <param name="predicate">アイテムが範囲内か判定する関数</param>
        /// <returns>フィルタリングされたコレクション</returns>
        public static IEnumerable<TItem> FilteringRange<TItem>(this IEnumerable<TItem> items,
            FilterType filterType,
            IReadOnlyDictionary<FilterType, (int Min, int Max)> filters,
            Func<TItem, int, int, bool> predicate)
        {
            if (!filters.TryGetValue(filterType, out var range))
                return items;

            return items.Where(x => predicate(x, range.Min, range.Max));
        }
    }
}