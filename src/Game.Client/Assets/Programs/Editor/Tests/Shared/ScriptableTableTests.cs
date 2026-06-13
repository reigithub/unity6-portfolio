using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Shared.Scriptable.Database;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Shared
{
    public class ScriptableTableTests
    {
        [System.Serializable]
        private class Rec
        {
            public int id;
            public string name;
            public Rec(int id, string name) { this.id = id; this.name = name; }
        }

        // 生成器を使わない手書きテーブル。基底のキー非依存コアを直接呼んで主キー検索を実装する
        // （＝生成器が自動化しているのと同じ配線を手書きで再現し、コアの正しさを検証する）。
        private class TestScriptableTable : ScriptableTable<Rec>
        {
            private static readonly Func<Rec, int> Sel = r => r.id;
            private static readonly IComparer<int> Cmp = Comparer<int>.Default;

            public Rec FindById(int id) => FindUnique(records, id, Sel, Cmp);
            public bool TryFindById(int id, out Rec record) => TryFindUnique(records, id, Sel, Cmp, out record);
            public Rec FindClosestById(int id, bool selectLower = true) => FindClosest(records, id, Sel, Cmp, selectLower);
            public ScriptableTableRecords<Rec> FindRangeById(int min, int max, bool ascending = true) => FindRange(records, min, max, Sel, Cmp, ascending);

            public void Set(params Rec[] rs) => records = rs;   // 主キー昇順で渡す

            public override void EditorSortAndValidate() => SortAndValidate(Sel, Cmp);
            public override bool EditorIsSorted() => IsSortedByKey(Sel, Cmp);
            public void Validate() => EditorSortAndValidate();   // 既存テストの呼び口を維持
        }

        private static TestScriptableTable Make(params Rec[] rs)
        {
            var t = ScriptableObject.CreateInstance<TestScriptableTable>();
            t.Set(rs);
            return t;
        }

        [Test]
        public void FindById_Existing_ReturnsRecord()
            => Assert.AreEqual("b", Make(new Rec(1, "a"), new Rec(3, "b")).FindById(3).name);

        [Test]
        public void FindById_Missing_Throws()
            => Assert.Throws<KeyNotFoundException>(() => Make(new Rec(1, "a"), new Rec(3, "b")).FindById(2));

        [Test]
        public void TryFindById_Existing_ReturnsTrue()
        {
            Assert.IsTrue(Make(new Rec(5, "x")).TryFindById(5, out var r));
            Assert.AreEqual("x", r.name);
        }

        [Test]
        public void TryFindById_Missing_ReturnsFalse()
        {
            Assert.IsFalse(Make(new Rec(1, "a")).TryFindById(9, out var r));
            Assert.IsNull(r);
        }

        [Test]
        public void FindRangeById_Inclusive()
        {
            var range = Make(new Rec(1, "a"), new Rec(2, "b"), new Rec(3, "c"), new Rec(4, "d")).FindRangeById(2, 3);
            Assert.AreEqual(2, range.Count);
            Assert.AreEqual("b", range[0].name);
            Assert.AreEqual("c", range[1].name);
        }

        [Test]
        public void FindRangeById_Descending()
        {
            var range = Make(new Rec(1, "a"), new Rec(2, "b"), new Rec(3, "c")).FindRangeById(1, 3, ascending: false);
            Assert.AreEqual("c", range[0].name);
            Assert.AreEqual("a", range[2].name);
        }

        [Test]
        public void FindRangeById_Empty()
            => Assert.AreEqual(0, Make(new Rec(1, "a"), new Rec(2, "b")).FindRangeById(10, 20).Count);

        [Test]
        public void FindClosestById_Lower()
            => Assert.AreEqual(3, Make(new Rec(1, "a"), new Rec(3, "c"), new Rec(5, "e")).FindClosestById(4, selectLower: true).id);

        [Test]
        public void FindClosestById_Upper()
            => Assert.AreEqual(5, Make(new Rec(1, "a"), new Rec(3, "c"), new Rec(5, "e")).FindClosestById(4, selectLower: false).id);

        [Test]
        public void FindClosestById_Exact()
            => Assert.AreEqual(3, Make(new Rec(1, "a"), new Rec(3, "c"), new Rec(5, "e")).FindClosestById(3).id);

        // floor/ceiling 境界：該当側に要素が無ければ null（MasterMemory 準拠、端へクランプしない）。
        [Test]
        public void FindClosestById_BelowAll_Lower_ReturnsNull()
            => Assert.IsNull(Make(new Rec(3, "c"), new Rec(5, "e")).FindClosestById(1, selectLower: true));

        [Test]
        public void FindClosestById_BelowAll_Upper_ReturnsFirst()
            => Assert.AreEqual(3, Make(new Rec(3, "c"), new Rec(5, "e")).FindClosestById(1, selectLower: false).id);

        [Test]
        public void FindClosestById_AboveAll_Upper_ReturnsNull()
            => Assert.IsNull(Make(new Rec(1, "a"), new Rec(3, "c")).FindClosestById(9, selectLower: false));

        [Test]
        public void FindClosestById_AboveAll_Lower_ReturnsLast()
            => Assert.AreEqual(3, Make(new Rec(1, "a"), new Rec(3, "c")).FindClosestById(9, selectLower: true).id);

        [Test]
        public void Linq_Where_Filters_OnAllView()
            => Assert.AreEqual(2, Make(new Rec(1, "a"), new Rec(2, "b"), new Rec(3, "a")).All.Where(r => r.name == "a").Count());

        [Test]
        public void Foreach_Iterates_OnAllView()
        {
            int n = 0;
            foreach (var _ in Make(new Rec(1, "a"), new Rec(2, "b")).All) n++;
            Assert.AreEqual(2, n);
        }

        [Test]
        public void AllReverse_IteratesDescending()
        {
            var t = Make(new Rec(1, "a"), new Rec(2, "b"), new Rec(3, "c"));
            Assert.AreEqual("c", t.AllReverse[0].name);
            Assert.AreEqual("a", t.AllReverse[2].name);
        }

        [Test]
        public void SortAndValidate_SortsAscending_PreservingAll()
        {
            var t = Make(new Rec(3, "c"), new Rec(1, "a"), new Rec(2, "b"));   // 未ソート入力
            t.Validate();
            Assert.AreEqual(3, t.All.Count);
            Assert.AreEqual(1, t.All[0].id);
            Assert.AreEqual(2, t.All[1].id);
            Assert.AreEqual(3, t.All[2].id);
        }

        [Test]
        public void SortAndValidate_RemovesNullSlots()
        {
            var t = Make(new Rec(2, "b"), null, new Rec(1, "a"));   // 空スロット混在
            t.Validate();
            Assert.AreEqual(2, t.All.Count);   // null は除去され切り詰められる
            Assert.AreEqual(1, t.All[0].id);
            Assert.AreEqual(2, t.All[1].id);
        }

        [Test]
        public void SortAndValidate_WarnsOnDuplicateKey()
        {
            var t = Make(new Rec(1, "a"), new Rec(1, "dup"), new Rec(2, "b"));
            LogAssert.Expect(LogType.Warning, new Regex("主キー 1 が重複"));
            t.Validate();
            Assert.AreEqual(3, t.All.Count);   // 重複は警告のみで除去はしない
        }

        [Test]
        public void EditorIsSorted_DetectsUnsorted_ThenSortFixes()
        {
            var t = Make(new Rec(3, "c"), new Rec(1, "a"), new Rec(2, "b"));   // 未整列
            Assert.IsFalse(t.EditorIsSorted());
            t.EditorSortAndValidate();
            Assert.IsTrue(t.EditorIsSorted());
            Assert.AreEqual(1, t.All[0].id);
        }
    }
}
