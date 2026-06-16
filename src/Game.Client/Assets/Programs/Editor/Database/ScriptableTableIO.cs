using System;
using System.IO;
using System.Text;
using Game.Shared.Scriptable.Database;
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
        // ScriptableTable 専用の入出力先（masterdata/raw の MasterMemory パイプラインとは非干渉）。
        // 無ければ作成してダイアログの初期ディレクトリ・書き込み先として使えるようにする。
        private static string DefaultDirectory()
        {
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "ProjectAssets", "Scriptable", "Database", "Raw"));
            Directory.CreateDirectory(dir);
            return dir;
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
                var (headers, rows) = table.EditorExportRows();
                var delimiter = ScriptableTableTextSerializer.DelimiterFromExtension(path);
                var text = ScriptableTableTextSerializer.WriteDocument(headers, rows, delimiter);
                ScriptableTableFileWriter.WriteWithBackup(path, text, new UTF8Encoding(false));
                Debug.Log($"[ScriptableTableIO] {rows.Count} 件を書き出しました: {path}", table);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ScriptableTableIO] エクスポートに失敗しました: {e}", table);
                EditorUtility.DisplayDialog("Export 失敗", e.Message, "OK");
            }
        }

        public static void Import(ScriptableTableBase table, ScriptableTableImportMode mode)
        {
            if (table == null) return;

            var path = EditorUtility.OpenFilePanel("Import Table from CSV/TSV", DefaultDirectory(), "tsv,csv");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                var text = File.ReadAllText(path);
                var delimiter = ScriptableTableTextSerializer.DelimiterFromExtension(path);
                var (headers, rows) = ScriptableTableTextSerializer.ParseDocument(text, delimiter);

                Undo.RecordObject(table, "Import Table");
                table.EditorImportRows(headers, rows, mode);
                EditorUtility.SetDirty(table);
                AssetDatabase.SaveAssets();
                Debug.Log($"[ScriptableTableIO] {rows.Count} 行を取り込みました（{mode}）: {path}", table);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ScriptableTableIO] インポートに失敗しました: {e}", table);
                EditorUtility.DisplayDialog("Import 失敗", e.Message, "OK");
            }
        }
    }
}
