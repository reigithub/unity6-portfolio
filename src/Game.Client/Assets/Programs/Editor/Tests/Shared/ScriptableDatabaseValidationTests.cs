using System;
using System.Collections.Generic;
using Game.Shared.Scriptable.Database;
using Game.Shared.Scriptable.Database.Validation;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.Shared
{
    /// <summary>
    /// 実際の ScriptableDatabase 資産に対するマスターデータ検証。
    /// テーブルごとに 1 ケースへ展開するため、Test Runner 上で個別実行・一括実行のどちらもできる。
    /// </summary>
    public class ScriptableDatabaseValidationTests
    {
        // 列挙できないこと自体を失敗として出す（0 ケース＝緑、にしない）。
        private static IEnumerable<TestCaseData> TableCases()
        {
            var database = Load();
            if (database == null)
            {
                yield return new TestCaseData((Type)null).SetName("Unresolved_Database");
                yield break;
            }

            var recordTypes = ValidationExecutor.WiredRecordTypes(database);
            if (recordTypes.Count == 0)
            {
                yield return new TestCaseData((Type)null).SetName("Empty_Tables");
                yield break;
            }

            foreach (var recordType in recordTypes)
            {
                yield return new TestCaseData(recordType).SetName(recordType.Name);
            }
        }

        [Test]
        public void Configuration_HasNoErrors()
        {
            var database = LoadDatabase();

            var result = ValidationExecutor.Create(database).ConfigurationResult;

            Assert.IsFalse(result.HasErrors, Describe(result));
        }

        [TestCaseSource(nameof(TableCases))]
        public void Table_HasNoValidationErrors(Type recordType)
        {
            Assert.IsNotNull(recordType,
                $"検証対象のテーブルを列挙できませんでした。{ScriptableDatabaseAssetPath.EditorAssetPath} を確認し、" +
                "ScriptableDatabaseWindow の Build / Register を実行してください。");

            var result = ValidationExecutor.Create(LoadDatabase()).Execute(recordType);

            Assert.IsFalse(result.HasErrors, Describe(result));
        }

        // TestCaseSource は Assert を使えないため、通知なしのロードと Assert 付きを分ける。
        private static ScriptableDatabase Load() =>
            AssetDatabase.LoadAssetAtPath<ScriptableDatabase>(ScriptableDatabaseAssetPath.EditorAssetPath);

        private static ScriptableDatabase LoadDatabase()
        {
            var database = Load();
            Assert.IsNotNull(database,
                $"{ScriptableDatabaseAssetPath.EditorAssetPath} が読み込めません。" +
                "ScriptableDatabaseWindow の Build / Register を実行してください。");

            return database;
        }

        private static string Describe(ValidationResult result) =>
            $"{result.Name}: {result.Errors.Count} 箇所でエラー（レコード数 {result.RecordCount}）\n{result.DescribeErrors()}";
    }
}
