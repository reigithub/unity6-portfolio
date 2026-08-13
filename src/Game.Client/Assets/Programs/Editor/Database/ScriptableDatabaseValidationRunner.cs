using System;
using System.Collections.Generic;
using Game.Shared.Scriptable.Database.Validation;
using UnityEngine;

namespace Game.Shared.Scriptable.Database.EditorTools
{
    /// <summary>
    /// マスターデータ検証をエディタから実行し、結果を Console へ整形出力するコマンド。
    /// 対象の解決は行わず資産を引数で受け取る（未生成・未登録時の案内は呼び出し側に一本化する）。
    /// </summary>
    public static class ScriptableDatabaseValidationRunner
    {
        /// <summary>
        /// 構成チェックと検証を実行する。エラーがあれば false。
        /// <paramref name="recordType"/> を指定すると、そのテーブルだけを検証する。
        /// </summary>
        public static bool Run(ScriptableObject database, Type recordType = null)
        {
            if (database == null)
            {
                Debug.LogError("[Validation] 対象の ScriptableDatabase がありません。先に ScriptableDatabaseWindow の 'Build' / 'Register' を実行してください。");
                return false;
            }

            IReadOnlyList<ValidationResult> results;
            try
            {
                var executor = ValidationExecutor.Create(database);
                results = recordType == null
                    ? executor.ExecuteAll()
                    : new[] { executor.ConfigurationResult, executor.Execute(recordType) };
            }
            catch (Exception e)
            {
                Debug.LogError($"[Validation] 検証を実行できません: {e}", database);
                return false;
            }

            return Report(results, database);
        }

        private static bool Report(IReadOnlyList<ValidationResult> results, ScriptableObject database)
        {
            int errorCount = 0;

            foreach (var result in results)
            {
                if (!result.HasErrors)
                {
                    Debug.Log($"[Validation] {Header(result)} OK", database);
                    continue;
                }

                errorCount++;
                Debug.LogError($"[Validation] {Header(result)} NG\n{result.DescribeErrors()}", database);
            }

            if (errorCount == 0)
            {
                Debug.Log($"[Validation] 完了: {results.Count} 件すべて OK。", database);
                return true;
            }

            Debug.LogError($"[Validation] 完了: {results.Count} 件中 {errorCount} 件でエラー。", database);
            return false;
        }

        /// <summary>結果 1 件分の見出し（Console 出力と検証ウィンドウで共通）。</summary>
        public static string Header(ValidationResult result)
        {
            string time = $"({result.CheckTime.TotalMilliseconds:0}ms)";

            if (result.Name == ValidationExecutor.ConfigurationResultName) return $"{result.Name} {time}";
            if (result.RecordCount < 0) return $"{result.Name}: 検証中断 {time}";

            return $"{result.Name}: {result.RecordCount}件 {time}";
        }
    }
}
