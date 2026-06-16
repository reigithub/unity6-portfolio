#if UNITY_EDITOR
using System.IO;
using System.Text;

namespace Game.Shared.Scriptable.Database
{
    /// <summary>
    /// ScriptableTable のエクスポート書き込みヘルパ（ファイル I/O のみ。Unity 非依存）。
    /// 既存ファイルは 1 世代 .bak へ退避しつつアトミックに置換し、誤エクスポートでの上書き消失と
    /// 書き込み途中中断によるファイル破損を防ぐ。
    /// </summary>
    public static class ScriptableTableFileWriter
    {
        /// <summary>
        /// <paramref name="path"/> へ <paramref name="contents"/> を書き込む。
        /// 既存ファイルがあれば <c>{path}.bak</c>（1 世代・毎回上書き）へ退避し、
        /// 一時ファイル経由で <see cref="File.Replace(string, string, string)"/> によりアトミックに置換する。
        /// </summary>
        public static void WriteWithBackup(string path, string contents, Encoding encoding)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            // 既存なし → 退避不要。単純書き込み。
            if (!File.Exists(path))
            {
                File.WriteAllText(path, contents, encoding);
                return;
            }

            var temp = path + ".tmp";
            var backup = path + ".bak";   // 1 世代。File.Replace が既存 .bak を上書きする。
            try
            {
                File.WriteAllText(temp, contents, encoding);
                // 既存を backup へ退避しつつ temp を path へアトミック置換（temp/path/backup は同一ディレクトリ）。
                File.Replace(temp, path, backup);
            }
            finally
            {
                // 置換失敗時に temp を残さない（成功時は Replace で temp が消費されるため no-op）。
                if (File.Exists(temp)) File.Delete(temp);
            }
        }
    }
}
#endif
