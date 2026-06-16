using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

        // ===== CSV/TSV インポート/エクスポート =====

        private enum Element { Fire, Water, Wind }

        // enum/bool/float を含む検証用レコード（主キーは [PrimaryKey] でマージ対象を特定）。
        [Serializable]
        private class Rec2
        {
            [PrimaryKey] public int Id { get; set; }
            public string Name { get; set; }
            public Element Kind { get; set; }
            public bool Active { get; set; }
            public float Weight { get; set; }

            public Rec2() { }   // Activator.CreateInstance 用の既定コンストラクタ

            public Rec2(int id, string name, Element kind, bool active, float weight)
            {
                Id = id; Name = name; Kind = kind; Active = active; Weight = weight;
            }
        }

        private class TestScriptableTable2 : ScriptableTable<Rec2>
        {
            private static readonly Func<Rec2, int> Sel = r => r.Id;
            private static readonly IComparer<int> Cmp = Comparer<int>.Default;

            public override void EditorSortAndValidate() => SortAndValidate(Sel, Cmp);
            public override bool EditorIsSorted() => IsSortedByKey(Sel, Cmp);
            public void Set(params Rec2[] rs) => records = rs;
        }

        private static TestScriptableTable2 MakeTable2(params Rec2[] rs)
        {
            var t = ScriptableObject.CreateInstance<TestScriptableTable2>();
            t.Set(rs);
            return t;
        }

        private static void AssertRec2Equal(Rec2 expected, Rec2 actual)
        {
            Assert.AreEqual(expected.Id, actual.Id);
            Assert.AreEqual(expected.Name, actual.Name);
            Assert.AreEqual(expected.Kind, actual.Kind);
            Assert.AreEqual(expected.Active, actual.Active);
            Assert.AreEqual(expected.Weight, actual.Weight);
        }

        // export → 文字列化 → 解析 → import(Replace) で全列が保持されることを各区切りで確認する。
        // import 後は主キー昇順に整列されるため、期待値も Id 昇順に並べて比較する（順序非依存）。
        private static void AssertRoundTrips(char delimiter, params Rec2[] source)
        {
            var src = MakeTable2(source);
            var (headers, rows) = src.EditorExportRows();
            var text = ScriptableTableTextSerializer.WriteDocument(headers, rows, delimiter);
            var (h2, r2) = ScriptableTableTextSerializer.ParseDocument(text, delimiter);

            var dst = MakeTable2();
            dst.EditorImportRows(h2, r2, mergeByPrimaryKey: false);

            var expected = source.OrderBy(r => r.Id).ToArray();
            Assert.AreEqual(expected.Length, dst.All.Count);
            for (int i = 0; i < expected.Length; i++) AssertRec2Equal(expected[i], dst.All[i]);
        }

        [Test]
        public void RoundTrip_Tsv_PreservesAllColumns()
            => AssertRoundTrips('\t',
                new Rec2(2, "beta", Element.Water, true, 2.5f),
                new Rec2(1, "alpha", Element.Fire, false, 0.25f));

        [Test]
        public void RoundTrip_Csv_PreservesAllColumns()
            => AssertRoundTrips(',',
                new Rec2(2, "beta", Element.Water, true, 2.5f),
                new Rec2(1, "alpha", Element.Fire, false, 0.25f));

        [Test]
        public void RoundTrip_Csv_EscapesCommaNewlineQuote()
            => AssertRoundTrips(',',
                new Rec2(1, "a,b", Element.Fire, true, 1f),
                new Rec2(2, "line1\nline2", Element.Water, false, 2f),
                new Rec2(3, "quote\"x", Element.Wind, true, 3f));

        [Test]
        public void ParseValue_Enum_ByName()
            => Assert.AreEqual((int)Element.Water, ScriptableTableTextSerializer.ParseValue(typeof(Element), "Water"));

        [Test]
        public void ParseValue_Bool_AcceptsNumericAndText()
        {
            Assert.AreEqual(true, ScriptableTableTextSerializer.ParseValue(typeof(bool), "1"));
            Assert.AreEqual(true, ScriptableTableTextSerializer.ParseValue(typeof(bool), "true"));
            Assert.AreEqual(false, ScriptableTableTextSerializer.ParseValue(typeof(bool), "0"));
        }

        [Test]
        public void ParseValue_Float_InvariantCulture()
            => Assert.AreEqual(1.5f, ScriptableTableTextSerializer.ParseValue(typeof(float), "1.5"));

        [Test]
        public void FormatValue_RoundTripConventions()
        {
            Assert.AreEqual("true", ScriptableTableTextSerializer.FormatValue(true));
            Assert.AreEqual("false", ScriptableTableTextSerializer.FormatValue(false));
            Assert.AreEqual("1.5", ScriptableTableTextSerializer.FormatValue(1.5f));
            Assert.AreEqual("Water", ScriptableTableTextSerializer.FormatValue(Element.Water));
            Assert.AreEqual(string.Empty, ScriptableTableTextSerializer.FormatValue(null));
        }

        [Test]
        public void Import_Replace_TotallyReplacesRecords()
        {
            var t = MakeTable2(
                new Rec2(1, "a", Element.Fire, true, 1f),
                new Rec2(2, "b", Element.Water, false, 2f),
                new Rec2(3, "c", Element.Wind, true, 3f));

            var headers = new[] { "Id", "Name", "Kind", "Active", "Weight" };
            var rows = new List<string[]> { new[] { "9", "z", "Fire", "true", "9" } };
            t.EditorImportRows(headers, rows, mergeByPrimaryKey: false);

            Assert.AreEqual(1, t.All.Count);
            Assert.AreEqual(9, t.All[0].Id);
        }

        [Test]
        public void Import_MergeByPrimaryKey_UpdatesAddsKeepsExisting()
        {
            var t = MakeTable2(
                new Rec2(1, "a", Element.Fire, true, 1f),
                new Rec2(2, "b", Element.Water, false, 2f),
                new Rec2(3, "c", Element.Wind, true, 3f));

            var headers = new[] { "Id", "Name", "Kind", "Active", "Weight" };
            var rows = new List<string[]>
            {
                new[] { "2", "B2", "Fire", "true", "2.5" },   // 既存キー → 更新
                new[] { "4", "d", "Wind", "false", "4" },     // 新規キー → 追加
            };
            t.EditorImportRows(headers, rows, mergeByPrimaryKey: true);

            Assert.AreEqual(4, t.All.Count);                  // 1,2,3 は保持＋4 追加
            Assert.AreEqual("B2", t.All[1].Name);             // id=2 が更新
            Assert.AreEqual(Element.Fire, t.All[1].Kind);
            Assert.AreEqual("c", t.All[2].Name);              // id=3 は保持
            Assert.AreEqual(4, t.All[3].Id);
        }

        [Test]
        public void Import_UnknownColumn_Warns()
        {
            var t = MakeTable2();
            var headers = new[] { "Id", "Bogus" };
            var rows = new List<string[]> { new[] { "5", "ignored" } };

            LogAssert.Expect(LogType.Warning, new Regex("未知の列"));
            t.EditorImportRows(headers, rows, mergeByPrimaryKey: false);

            Assert.AreEqual(1, t.All.Count);
            Assert.AreEqual(5, t.All[0].Id);
        }

        [Test]
        public void Import_InvalidValue_Throws()
        {
            var t = MakeTable2();
            var headers = new[] { "Id" };
            var rows = new List<string[]> { new[] { "not-an-int" } };

            Assert.Throws<FormatException>(
                () => t.EditorImportRows(headers, rows, mergeByPrimaryKey: false));
        }

        [Test]
        public void Import_SortsAfterImport()
        {
            var t = MakeTable2();
            var headers = new[] { "Id", "Name", "Kind", "Active", "Weight" };
            var rows = new List<string[]>
            {
                new[] { "3", "c", "Wind", "true", "3" },
                new[] { "1", "a", "Fire", "true", "1" },
                new[] { "2", "b", "Water", "true", "2" },
            };
            t.EditorImportRows(headers, rows, mergeByPrimaryKey: false);

            Assert.IsTrue(t.EditorIsSorted());
            Assert.AreEqual(1, t.All[0].Id);
            Assert.AreEqual(3, t.All[2].Id);
        }

        // ===== エクスポート書き込み（1世代 .bak ＋アトミック置換） =====

        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        // 一時ディレクトリを作り、action 実行後に必ず後始末する。
        private static void InTempDir(Action<string> action)
        {
            var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            try { action(dir); }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }

        [Test]
        public void WriteWithBackup_NewFile_WritesWithoutBackup()
        {
            InTempDir(dir =>
            {
                var path = Path.Combine(dir, "table.tsv");
                ScriptableTableFileWriter.WriteWithBackup(path, "v1", Utf8NoBom);

                Assert.AreEqual("v1", File.ReadAllText(path));
                Assert.IsFalse(File.Exists(path + ".bak"));   // 既存なし → .bak は作らない
                Assert.IsFalse(File.Exists(path + ".tmp"));   // temp を残さない
            });
        }

        [Test]
        public void WriteWithBackup_ExistingFile_BacksUpOldContent()
        {
            InTempDir(dir =>
            {
                var path = Path.Combine(dir, "table.tsv");
                File.WriteAllText(path, "old", Utf8NoBom);

                ScriptableTableFileWriter.WriteWithBackup(path, "new", Utf8NoBom);

                Assert.AreEqual("new", File.ReadAllText(path));        // 本体は更新
                Assert.AreEqual("old", File.ReadAllText(path + ".bak")); // 旧内容が退避
                Assert.IsFalse(File.Exists(path + ".tmp"));
            });
        }

        [Test]
        public void WriteWithBackup_TwiceOverwrite_BakKeepsOnlyPrevious()
        {
            InTempDir(dir =>
            {
                var path = Path.Combine(dir, "table.tsv");
                File.WriteAllText(path, "v1", Utf8NoBom);

                ScriptableTableFileWriter.WriteWithBackup(path, "v2", Utf8NoBom);
                ScriptableTableFileWriter.WriteWithBackup(path, "v3", Utf8NoBom);

                Assert.AreEqual("v3", File.ReadAllText(path));         // 最新
                Assert.AreEqual("v2", File.ReadAllText(path + ".bak")); // 1世代＝直前のみ
            });
        }

        // ===== table ⇔ ファイル変換コア（一括処理が共用） =====

        [Test]
        public void FileIO_RoundTrip_Tsv()
        {
            InTempDir(dir =>
            {
                var src = MakeTable2(
                    new Rec2(2, "b", Element.Water, true, 2.5f),
                    new Rec2(1, "a", Element.Fire, false, 0.25f));
                var path = Path.Combine(dir, "t.tsv");
                ScriptableTableFileIO.ExportToFile(src, path, Utf8NoBom);

                var dst = MakeTable2();
                ScriptableTableFileIO.ImportFromFile(dst, path, mergeByPrimaryKey: false);

                var expected = new[]
                {
                    new Rec2(1, "a", Element.Fire, false, 0.25f),
                    new Rec2(2, "b", Element.Water, true, 2.5f),
                };
                Assert.AreEqual(expected.Length, dst.All.Count);
                for (int i = 0; i < expected.Length; i++) AssertRec2Equal(expected[i], dst.All[i]);
            });
        }

        [Test]
        public void FileIO_RoundTrip_Csv()
        {
            InTempDir(dir =>
            {
                var src = MakeTable2(new Rec2(1, "a,b", Element.Fire, true, 1f));   // カンマ含む → CSV エスケープ
                var path = Path.Combine(dir, "t.csv");
                ScriptableTableFileIO.ExportToFile(src, path, Utf8NoBom);

                var dst = MakeTable2();
                ScriptableTableFileIO.ImportFromFile(dst, path, mergeByPrimaryKey: false);

                Assert.AreEqual(1, dst.All.Count);
                AssertRec2Equal(new Rec2(1, "a,b", Element.Fire, true, 1f), dst.All[0]);
            });
        }

        [Test]
        public void FileIO_ImportFromFile_Merge()
        {
            InTempDir(dir =>
            {
                var fileTable = MakeTable2(
                    new Rec2(2, "B2", Element.Fire, true, 2.5f),   // 既存キー → 更新
                    new Rec2(4, "d", Element.Wind, false, 4f));    // 新規キー → 追加
                var path = Path.Combine(dir, "t.tsv");
                ScriptableTableFileIO.ExportToFile(fileTable, path, Utf8NoBom);

                var target = MakeTable2(
                    new Rec2(1, "a", Element.Fire, true, 1f),
                    new Rec2(2, "b", Element.Water, false, 2f),
                    new Rec2(3, "c", Element.Wind, true, 3f));
                ScriptableTableFileIO.ImportFromFile(target, path, mergeByPrimaryKey: true);

                Assert.AreEqual(4, target.All.Count);          // 1,2,3 保持＋4 追加
                Assert.AreEqual("B2", target.All[1].Name);     // id=2 更新
                Assert.AreEqual(4, target.All[3].Id);          // id=4 追加
            });
        }

        [Test]
        public void FileIO_ExportToFile_BacksUpExisting()
        {
            InTempDir(dir =>
            {
                var path = Path.Combine(dir, "t.tsv");
                ScriptableTableFileIO.ExportToFile(MakeTable2(new Rec2(1, "a", Element.Fire, true, 1f)), path, Utf8NoBom);
                ScriptableTableFileIO.ExportToFile(MakeTable2(new Rec2(2, "b", Element.Water, false, 2f)), path, Utf8NoBom);

                Assert.IsTrue(File.Exists(path + ".bak"));   // 一括でも既存ファイルは .bak へ退避される
            });
        }
    }
}
