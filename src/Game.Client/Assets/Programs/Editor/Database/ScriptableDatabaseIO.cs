using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Game.Shared.Scriptable.Database.EditorTools
{
    /// <summary>
    /// ScriptableDatabase 配下の全テーブルを一括で CSV/TSV 入出力するエディタ機能。
    /// テーブル列挙はリフレクション（ScriptableTableBase フィールド）で行い、ScriptableDatabase 型へ
    /// コンパイル時依存しない（未生成でも壊れないビルダー方針と同じ）。ファイル名規約は {TableType.Name}.{ext}。
    /// 文字列⇔ファイルの変換は <see cref="ScriptableTableFileIO"/>、副作用（ダイアログ/Undo/保存）はここで担う。
    /// </summary>
    public static class ScriptableDatabaseIO
    {
        // ---- 一括処理本体（ScriptableDatabase 型に依存せず ScriptableObject + リフレクションで扱う） ----

        public static void BatchExport(ScriptableObject database, string extension)
        {
            if (database == null) return;

            var dir = EditorUtility.SaveFolderPanel("Export All Tables", ScriptableTableIO.DefaultDirectory(), string.Empty);
            if (string.IsNullOrEmpty(dir)) return;

            try
            {
                int n = 0;
                foreach (var table in Tables(database))
                {
                    var path = Path.Combine(dir, table.GetType().Name + "." + extension);
                    ScriptableTableFileIO.ExportToFile(table, path, ScriptableTableIO.Utf8NoBom);
                    n++;
                }
                Debug.Log($"[ScriptableDatabaseIO] {n} テーブルを書き出しました（{extension.ToUpperInvariant()}）: {dir}", database);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ScriptableDatabaseIO] 一括エクスポートに失敗しました: {e}", database);
                EditorUtility.DisplayDialog("Export All 失敗", e.Message, "OK");
            }
        }

        public static void BatchImport(ScriptableObject database, string extension, bool mergeByPrimaryKey)
        {
            if (database == null) return;

            var dir = EditorUtility.OpenFolderPanel("Import All Tables", ScriptableTableIO.DefaultDirectory(), string.Empty);
            if (string.IsNullOrEmpty(dir)) return;

            try
            {
                int imported = 0, skipped = 0;
                foreach (var table in Tables(database))
                {
                    var path = Path.Combine(dir, table.GetType().Name + "." + extension);
                    if (!File.Exists(path))
                    {
                        skipped++;
                        Debug.LogWarning($"[ScriptableDatabaseIO] {Path.GetFileName(path)} が見つかりません。スキップします。", table);
                        continue;
                    }

                    Undo.RecordObject(table, "Import All Tables");
                    ScriptableTableFileIO.ImportFromFile(table, path, mergeByPrimaryKey);
                    EditorUtility.SetDirty(table);
                    imported++;
                }

                AssetDatabase.SaveAssets();
                Debug.Log($"[ScriptableDatabaseIO] 取り込み {imported} 件 / スキップ {skipped} 件" +
                          $"（{(mergeByPrimaryKey ? "Merge" : "Replace")}, {extension.ToUpperInvariant()}）: {dir}", database);

                // 取り込みはマスターデータの主要な投入経路のため、そのまま検証まで通す（取り込み自体は巻き戻さない）。
                ScriptableDatabaseValidationRunner.Run(database);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ScriptableDatabaseIO] 一括インポートに失敗しました: {e}", database);
                EditorUtility.DisplayDialog("Import All 失敗", e.Message, "OK");
            }
        }

        // database が保持する ScriptableTableBase フィールドを列挙する（null/未結線は警告してスキップ）。
        private static IEnumerable<ScriptableTableBase> Tables(ScriptableObject database)
        {
            var fields = database.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (var field in fields)
            {
                if (!typeof(ScriptableTableBase).IsAssignableFrom(field.FieldType)) continue;

                if (field.GetValue(database) is ScriptableTableBase table)
                {
                    yield return table;
                }
                else
                {
                    Debug.LogWarning($"[ScriptableDatabaseIO] フィールド '{field.Name}' が未結線です。スキップします。（Register を実行してください）", database);
                }
            }
        }

        // ---- 対象 ScriptableDatabase の解決（ScriptableDatabaseWindow から利用） ----

        /// <summary>
        /// 固定パスの ScriptableDatabase.asset をロードする（型はリフレクションで解決）。
        /// 未生成・未登録でも通知しないので、通知が必要な経路は <see cref="RunWithDatabase"/> を使う。
        /// </summary>
        internal static ScriptableObject LoadDatabaseOrNull()
        {
            var dbType = ScriptableDatabaseBuilder.FindDatabaseType();
            return dbType == null
                ? null
                : AssetDatabase.LoadAssetAtPath(ScriptableDatabaseAssetPath.EditorAssetPath, dbType) as ScriptableObject;
        }

        // ロードしてアクションを実行する。解決できない場合は理由を通知して実行しない。
        internal static void RunWithDatabase(Action<ScriptableObject> action)
        {
            if (ScriptableDatabaseBuilder.FindDatabaseType() == null)
            {
                Debug.LogError("[ScriptableDatabaseIO] ScriptableDatabase 型が見つかりません。先に ScriptableDatabaseWindow の 'Build' を実行してください。");
                return;
            }

            var database = LoadDatabaseOrNull();
            if (database == null)
            {
                Debug.LogError($"[ScriptableDatabaseIO] {ScriptableDatabaseAssetPath.EditorAssetPath} が見つかりません。先に ScriptableDatabaseWindow の 'Register' を実行してください。");
                return;
            }

            action(database);
        }
    }
}
