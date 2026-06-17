using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Game.Shared.Scriptable.Database.EditorTools
{
    /// <summary>
    /// ScriptableTable の CSV/TSV インポート/エクスポートを、ファイルダイアログ＋ファイル I/O 経由で実行する薄いファサード。
    /// 文字列 ⇔ 行列の変換と records への反映は <see cref="ScriptableTableTextSerializer"/> と
    /// <see cref="ScriptableTableBase"/> 側に委ね、ここは UI とファイル入出力に専念する。
    /// </summary>
    public static class ScriptableTableIO
    {
        /// <summary>UTF-8 (BOM なし)。一括側とも共有する出力エンコーディング。</summary>
        internal static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        // ScriptableTable 専用の入出力先（masterdata/raw の MasterMemory パイプラインとは非干渉）。
        // 無ければ作成してダイアログの初期ディレクトリ・書き込み先として使えるようにする。
        internal static string DefaultDirectory()
        {
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "ProjectAssets", "Scriptable", "Database", "Raw"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static void Import(ScriptableTableBase table, bool mergeByPrimaryKey)
        {
            if (table == null) return;

            var path = EditorUtility.OpenFilePanel("Import Table from CSV/TSV", DefaultDirectory(), "tsv,csv");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                Undo.RecordObject(table, "Import Table");
                ScriptableTableFileIO.ImportFromFile(table, path, mergeByPrimaryKey);
                EditorUtility.SetDirty(table);
                AssetDatabase.SaveAssets();
                Debug.Log($"[ScriptableTableIO] 取り込みました（{(mergeByPrimaryKey ? "Merge" : "Replace")}）: {path}", table);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ScriptableTableIO] インポートに失敗しました: {e}", table);
                EditorUtility.DisplayDialog("Import 失敗", e.Message, "OK");
            }
        }

        // extension は "tsv" / "csv"。SaveFilePanel は単一拡張子しか扱えないため形式ごとに呼び分ける。
        public static void Export(ScriptableTableBase table, string extension)
        {
            if (table == null) return;

            var defaultName = table.GetType().Name + "." + extension;
            var path = EditorUtility.SaveFilePanel("Export Table", DefaultDirectory(), defaultName, extension);
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                ScriptableTableFileIO.ExportToFile(table, path, Utf8NoBom);
                Debug.Log($"[ScriptableTableIO] 書き出しました: {path}", table);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ScriptableTableIO] エクスポートに失敗しました: {e}", table);
                EditorUtility.DisplayDialog("Export 失敗", e.Message, "OK");
            }
        }
    }
}
