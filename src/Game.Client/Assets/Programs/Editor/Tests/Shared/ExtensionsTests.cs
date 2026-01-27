using System.Collections.Generic;
using System.Linq;
using Game.Shared.Extensions;
using NUnit.Framework;

namespace Game.Tests.Shared
{
    [TestFixture]
    public class ExtensionsTests
    {
        #region StringFormatExtensions Tests

        [TestFixture]
        public class StringFormatExtensionsTests
        {
            [Test]
            public void FormatToTimer_Int_ZeroSeconds()
            {
                Assert.That(0.FormatToTimer(), Is.EqualTo("00:00"));
            }

            [Test]
            public void FormatToTimer_Int_SecondsOnly()
            {
                Assert.That(30.FormatToTimer(), Is.EqualTo("00:30"));
            }

            [Test]
            public void FormatToTimer_Int_OneMinute()
            {
                Assert.That(60.FormatToTimer(), Is.EqualTo("01:00"));
            }

            [Test]
            public void FormatToTimer_Int_MinutesAndSeconds()
            {
                Assert.That(90.FormatToTimer(), Is.EqualTo("01:30"));
            }

            [Test]
            public void FormatToTimer_Int_TenMinutes()
            {
                Assert.That(600.FormatToTimer(), Is.EqualTo("10:00"));
            }

            [Test]
            public void FormatToTimer_Int_MaxValues()
            {
                Assert.That(3599.FormatToTimer(), Is.EqualTo("59:59"));
            }

            [Test]
            public void FormatToTimer_Int_OverOneHour()
            {
                Assert.That(3661.FormatToTimer(), Is.EqualTo("61:01"));
            }

            [Test]
            public void FormatToTimer_Float_TruncatesDecimals()
            {
                Assert.That(90.9f.FormatToTimer(), Is.EqualTo("01:30"));
            }

            [Test]
            public void FormatToTimer_Float_ZeroPointFive()
            {
                Assert.That(0.5f.FormatToTimer(), Is.EqualTo("00:00"));
            }
        }

        #endregion

        #region MasterDataConversionExtensions Tests

        [TestFixture]
        public class MasterDataConversionExtensionsTests
        {
            #region ToRate Tests

            [Test]
            public void ToRate_Zero_ReturnsZero()
            {
                Assert.That(0.ToRate(), Is.EqualTo(0f));
            }

            [Test]
            public void ToRate_10000_ReturnsOne()
            {
                Assert.That(10000.ToRate(), Is.EqualTo(1f).Within(0.0001f));
            }

            [Test]
            public void ToRate_5000_ReturnsPointFive()
            {
                Assert.That(5000.ToRate(), Is.EqualTo(0.5f).Within(0.0001f));
            }

            [Test]
            public void ToRate_1_ReturnsPointZeroZeroZeroOne()
            {
                Assert.That(1.ToRate(), Is.EqualTo(0.0001f).Within(0.00001f));
            }

            [Test]
            public void ToRate_20000_ReturnsTwo()
            {
                Assert.That(20000.ToRate(), Is.EqualTo(2f).Within(0.0001f));
            }

            [Test]
            public void ToRate_NegativeValue()
            {
                Assert.That((-5000).ToRate(), Is.EqualTo(-0.5f).Within(0.0001f));
            }

            #endregion

            #region ToPercent Tests

            [Test]
            public void ToPercent_Zero_ReturnsZero()
            {
                Assert.That(0.ToPercent(), Is.EqualTo(0f));
            }

            [Test]
            public void ToPercent_10000_Returns100()
            {
                Assert.That(10000.ToPercent(), Is.EqualTo(100f).Within(0.01f));
            }

            [Test]
            public void ToPercent_5000_Returns50()
            {
                Assert.That(5000.ToPercent(), Is.EqualTo(50f).Within(0.01f));
            }

            [Test]
            public void ToPercent_100_Returns1()
            {
                Assert.That(100.ToPercent(), Is.EqualTo(1f).Within(0.01f));
            }

            #endregion

            #region ToSeconds Tests

            [Test]
            public void ToSeconds_Zero_ReturnsZero()
            {
                Assert.That(0.ToSeconds(), Is.EqualTo(0f));
            }

            [Test]
            public void ToSeconds_1000_ReturnsOne()
            {
                Assert.That(1000.ToSeconds(), Is.EqualTo(1f).Within(0.001f));
            }

            [Test]
            public void ToSeconds_500_ReturnsPointFive()
            {
                Assert.That(500.ToSeconds(), Is.EqualTo(0.5f).Within(0.001f));
            }

            [Test]
            public void ToSeconds_2500_Returns2Point5()
            {
                Assert.That(2500.ToSeconds(), Is.EqualTo(2.5f).Within(0.001f));
            }

            #endregion

            #region ToUnit Tests

            [Test]
            public void ToUnit_Zero_ReturnsZero()
            {
                Assert.That(0.ToUnit(), Is.EqualTo(0f));
            }

            [Test]
            public void ToUnit_1000_ReturnsOne()
            {
                Assert.That(1000.ToUnit(), Is.EqualTo(1f).Within(0.001f));
            }

            [Test]
            public void ToUnit_3500_Returns3Point5()
            {
                Assert.That(3500.ToUnit(), Is.EqualTo(3.5f).Within(0.001f));
            }

            #endregion

            #region ToScale Tests

            [Test]
            public void ToScale_Zero_ReturnsZero()
            {
                Assert.That(0.ToScale(), Is.EqualTo(0f));
            }

            [Test]
            public void ToScale_100_ReturnsOne()
            {
                Assert.That(100.ToScale(), Is.EqualTo(1f).Within(0.01f));
            }

            [Test]
            public void ToScale_150_ReturnsOnePointFive()
            {
                Assert.That(150.ToScale(), Is.EqualTo(1.5f).Within(0.01f));
            }

            [Test]
            public void ToScale_50_ReturnsPointFive()
            {
                Assert.That(50.ToScale(), Is.EqualTo(0.5f).Within(0.01f));
            }

            #endregion
        }

        #endregion

        #region SortExtensions Tests

        [TestFixture]
        public class SortExtensionsTests
        {
            private class TestItem
            {
                public int Value { get; set; }
                public string Name { get; set; }
            }

            [Test]
            public void Sorting_Ascending_SortsCorrectly()
            {
                // Arrange
                var items = new[]
                {
                    new TestItem { Value = 3 },
                    new TestItem { Value = 1 },
                    new TestItem { Value = 2 }
                };

                // Act
                var result = items.Sorting(OrderType.Ascending, x => x.Value).ToArray();

                // Assert
                Assert.That(result[0].Value, Is.EqualTo(1));
                Assert.That(result[1].Value, Is.EqualTo(2));
                Assert.That(result[2].Value, Is.EqualTo(3));
            }

            [Test]
            public void Sorting_Descending_SortsCorrectly()
            {
                // Arrange
                var items = new[]
                {
                    new TestItem { Value = 1 },
                    new TestItem { Value = 3 },
                    new TestItem { Value = 2 }
                };

                // Act
                var result = items.Sorting(OrderType.Descending, x => x.Value).ToArray();

                // Assert
                Assert.That(result[0].Value, Is.EqualTo(3));
                Assert.That(result[1].Value, Is.EqualTo(2));
                Assert.That(result[2].Value, Is.EqualTo(1));
            }

            [Test]
            public void Sorting_None_ReturnsOriginalOrder()
            {
                // Arrange
                var items = new[]
                {
                    new TestItem { Value = 3 },
                    new TestItem { Value = 1 },
                    new TestItem { Value = 2 }
                };

                // Act
                var result = items.Sorting(OrderType.None, x => x.Value).ToArray();

                // Assert
                Assert.That(result[0].Value, Is.EqualTo(3));
                Assert.That(result[1].Value, Is.EqualTo(1));
                Assert.That(result[2].Value, Is.EqualTo(2));
            }

            [Test]
            public void Sorting_ByString_SortsAlphabetically()
            {
                // Arrange
                var items = new[]
                {
                    new TestItem { Name = "Charlie" },
                    new TestItem { Name = "Alice" },
                    new TestItem { Name = "Bob" }
                };

                // Act
                var result = items.Sorting(OrderType.Ascending, x => x.Name).ToArray();

                // Assert
                Assert.That(result[0].Name, Is.EqualTo("Alice"));
                Assert.That(result[1].Name, Is.EqualTo("Bob"));
                Assert.That(result[2].Name, Is.EqualTo("Charlie"));
            }

            [Test]
            public void Sorting_EmptyCollection_ReturnsEmpty()
            {
                // Arrange
                var items = new TestItem[0];

                // Act
                var result = items.Sorting(OrderType.Ascending, x => x.Value).ToArray();

                // Assert
                Assert.That(result, Is.Empty);
            }
        }

        #endregion

        #region FilterExtensions Tests

        [TestFixture]
        public class FilterExtensionsTests
        {
            private class TestItem
            {
                public int Category { get; set; }
                public int[] Tags { get; set; }
                public int Level { get; set; }
            }

            [Test]
            public void Filtering_MatchingValues_ReturnsFiltered()
            {
                // Arrange
                var items = new[]
                {
                    new TestItem { Category = 1 },
                    new TestItem { Category = 2 },
                    new TestItem { Category = 1 },
                    new TestItem { Category = 3 }
                };
                var filters = new Dictionary<FilterType, HashSet<int>>
                {
                    { FilterType.Language, new HashSet<int> { 1 } }
                };

                // Act
                var result = items.Filtering(FilterType.Language, filters, (item, value) => item.Category == value).ToArray();

                // Assert
                Assert.That(result.Length, Is.EqualTo(2));
                Assert.That(result.All(x => x.Category == 1), Is.True);
            }

            [Test]
            public void Filtering_NoMatchingFilter_ReturnsAllItems()
            {
                // Arrange
                var items = new[]
                {
                    new TestItem { Category = 1 },
                    new TestItem { Category = 2 }
                };
                var filters = new Dictionary<FilterType, HashSet<int>>();

                // Act
                var result = items.Filtering(FilterType.Language, filters, (item, value) => item.Category == value).ToArray();

                // Assert
                Assert.That(result.Length, Is.EqualTo(2));
            }

            [Test]
            public void Filtering_MultipleFilterValues_MatchesAny()
            {
                // Arrange
                var items = new[]
                {
                    new TestItem { Category = 1 },
                    new TestItem { Category = 2 },
                    new TestItem { Category = 3 },
                    new TestItem { Category = 4 }
                };
                var filters = new Dictionary<FilterType, HashSet<int>>
                {
                    { FilterType.Elements, new HashSet<int> { 1, 3 } }
                };

                // Act
                var result = items.Filtering(FilterType.Elements, filters, (item, value) => item.Category == value).ToArray();

                // Assert
                Assert.That(result.Length, Is.EqualTo(2));
                Assert.That(result.Select(x => x.Category), Is.EquivalentTo(new[] { 1, 3 }));
            }

            [Test]
            public void FilteringAll_MatchesAllValues()
            {
                // Arrange
                var items = new[]
                {
                    new TestItem { Tags = new[] { 1, 2, 3 } },
                    new TestItem { Tags = new[] { 1, 2 } },
                    new TestItem { Tags = new[] { 1, 3 } }
                };
                var filters = new Dictionary<FilterType, HashSet<int>>
                {
                    { FilterType.Elements, new HashSet<int> { 1, 2 } }
                };

                // Act
                var result = items.FilteringAll(FilterType.Elements, filters, (item, value) => item.Tags.Contains(value)).ToArray();

                // Assert
                Assert.That(result.Length, Is.EqualTo(2));
            }

            [Test]
            public void FilteringRange_WithinRange_ReturnsMatching()
            {
                // Arrange
                var items = new[]
                {
                    new TestItem { Level = 5 },
                    new TestItem { Level = 10 },
                    new TestItem { Level = 15 },
                    new TestItem { Level = 20 }
                };
                var filters = new Dictionary<FilterType, (int Min, int Max)>
                {
                    { FilterType.Language, (10, 15) }
                };

                // Act
                var result = items.FilteringRange(FilterType.Language, filters, (item, min, max) => item.Level >= min && item.Level <= max).ToArray();

                // Assert
                Assert.That(result.Length, Is.EqualTo(2));
                Assert.That(result.Select(x => x.Level), Is.EquivalentTo(new[] { 10, 15 }));
            }

            [Test]
            public void FilteringRange_NoFilter_ReturnsAll()
            {
                // Arrange
                var items = new[]
                {
                    new TestItem { Level = 5 },
                    new TestItem { Level = 10 }
                };
                var filters = new Dictionary<FilterType, (int Min, int Max)>();

                // Act
                var result = items.FilteringRange(FilterType.Language, filters, (item, min, max) => item.Level >= min && item.Level <= max).ToArray();

                // Assert
                Assert.That(result.Length, Is.EqualTo(2));
            }
        }

        #endregion
    }
}
