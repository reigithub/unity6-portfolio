using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core.Extensions
{
    public enum SortType
    {
        None,
        Attack,
        Defense,
    }

    public enum OrderType
    {
        None,
        Ascending,
        Descending
    }

    public enum FilterType
    {
        None,
        Language,
        Elements,
        DoubleElements,
    }

    /// <summary>
    /// WIP
    /// </summary>
    public static class SortExtensions
    {
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

    public static class FilterExtensions
    {
        public static IEnumerable<TItem> Filtering<TItem>(this IEnumerable<TItem> items,
            FilterType filterType,
            IReadOnlyDictionary<FilterType, HashSet<int>> filters,
            Func<TItem, int, bool> predicate)
        {
            if (!filters.TryGetValue(filterType, out var values))
                return items;

            return items.Where(x => values.Any(y => predicate(x, y)));
        }

        public static IEnumerable<TItem> FilteringAll<TItem>(this IEnumerable<TItem> items,
            FilterType filterType,
            IReadOnlyDictionary<FilterType, HashSet<int>> filters,
            Func<TItem, int, bool> predicate)
        {
            if (!filters.TryGetValue(filterType, out var values))
                return items;

            return items.Where(x => values.All(y => predicate(x, y)));
        }

        public static IEnumerable<TItem> FilteringMultiple<TItem>(this IEnumerable<TItem> items,
            FilterType filterType,
            IReadOnlyDictionary<FilterType, HashSet<int>> filters,
            Func<TItem, int[], bool> predicate)
        {
            if (!filters.TryGetValue(filterType, out var values))
                return items;

            return items.Where(x => predicate(x, values.ToArray()));
        }

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