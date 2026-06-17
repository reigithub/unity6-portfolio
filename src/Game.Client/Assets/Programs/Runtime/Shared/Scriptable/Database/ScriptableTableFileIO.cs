#if UNITY_EDITOR
using System.IO;
using System.Text;

namespace Game.Shared.Scriptable.Database
{
    /// <summary>
    /// ScriptableTable と CSV/TSV ファイルの相互変換コア（ダイアログ非依存・副作用最小）。
    /// 区切りは拡張子（.csv/.tsv）で判定する。Undo/SetDirty/SaveAssets/ダイアログ等の
    /// UnityEditor 副作用は呼び出し側（Editor のファサード）の責務とし、ここには持ち込まない。
    /// 単一テーブル（ScriptableTableIO）と一括処理（ScriptableDatabaseIO）の双方から共用する。
    /// </summary>
    public static class ScriptableTableFileIO
    {
        /// <summary>table の内容を <paramref name="path"/> へ書き出す（拡張子で CSV/TSV 判定、既存は .bak 退避）。</summary>
        public static void ExportToFile(ScriptableTableBase table, string path, Encoding encoding)
        {
            var delimiter = ScriptableTableTextSerializer.DelimiterFromExtension(path);
            var (headers, rows) = table.EditorExportRows();
            var text = ScriptableTableTextSerializer.WriteDocument(headers, rows, delimiter);
            ScriptableTableFileWriter.WriteWithBackup(path, text, encoding);
        }

        /// <summary>
        /// <paramref name="path"/> の CSV/TSV を table へ取り込む（拡張子で判定）。
        /// records への反映のみ行い、Undo 記録・ダーティ化・保存は呼び出し側に委ねる。
        /// </summary>
        public static void ImportFromFile(ScriptableTableBase table, string path, bool mergeByPrimaryKey)
        {
            var text = File.ReadAllText(path);
            var delimiter = ScriptableTableTextSerializer.DelimiterFromExtension(path);
            var (headers, rows) = ScriptableTableTextSerializer.ParseDocument(text, delimiter);
            table.EditorImportRows(headers, rows, mergeByPrimaryKey);
        }
    }
}
#endif
