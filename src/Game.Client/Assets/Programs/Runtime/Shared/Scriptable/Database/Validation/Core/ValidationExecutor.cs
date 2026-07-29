#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Game.Shared.Scriptable.Database.Validation
{
    /// <summary>
    /// マスターデータ検証の実行器。状態はすべてインスタンスが持つため、
    /// エディタから何度生成しても前回の登録内容が残ることはない。
    /// テーブル 1 つの失敗が他のテーブルの検証を止めないよう、例外はテーブル単位で結果へ畳み込む。
    /// </summary>
    public sealed class ValidationExecutor
    {
        /// <summary>構成チェック結果の表示名。テーブル検証の結果と区別するために使う。</summary>
        public const string ConfigurationResultName = "(構成)";

        private static readonly MethodInfo _executeCoreMethod =
            typeof(ValidationExecutor).GetMethod(nameof(ExecuteCore), BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly IRecordGetter _recordGetter;
        private readonly List<ITableValidator> _tableValidators;

        // レコード型 → List<IRecordValidator<TRecord>> / List<ITableRecordsValidator<TRecord>>。
        // ExecuteCore<T> がそのままキャストして使う。
        private readonly Dictionary<Type, IList> _recordValidators = new();
        private readonly Dictionary<Type, IList> _recordsValidators = new();

        // 見落とし報告の重複を避ける（同じ型・同じ種別の validator が複数あっても報告は 1 件）。
        private readonly HashSet<(Type RecordType, string Kind)> _reportedOrphans = new();

        /// <summary>検証対象のレコード型。</summary>
        public IReadOnlyList<Type> RecordTypes { get; }

        /// <summary>構成チェック（テーブル結線・生成漏れ・宣言の妥当性）の結果。</summary>
        public ValidationResult ConfigurationResult { get; }

        /// <summary>
        /// ScriptableDatabase 資産を対象に構築する。
        /// 生成コンテナ型へコンパイル時依存しないよう <see cref="ScriptableObject"/> で受け、テーブルは走査で解決する。
        /// </summary>
        public static ValidationExecutor Create(ScriptableObject database)
        {
            var schema = ScriptableDatabaseSchema.Inspect(database);

            return new ValidationExecutor(
                schema.Tables.Keys.ToList(),
                new ScriptableDatabaseRecordGetter(schema.Tables),
                schema.Validators,
                DiscoverValidators(schema.Result),
                schema.Result);
        }

        /// <summary>
        /// 対象・供給・validator を直接与えて構築する（資産に依存しないエンジン検証や、限定実行用）。
        /// 検証属性の宣言由来の validator は資産経路と同じく自動で組み込むが、validator クラスの自動発見は行わない。
        /// </summary>
        public static ValidationExecutor Create(IReadOnlyList<Type> recordTypes, IRecordGetter recordGetter, IReadOnlyList<object> validators = null)
        {
            if (recordTypes == null) throw new ArgumentNullException(nameof(recordTypes));

            var configurationResult = new ValidationResult(ConfigurationResultName, 0);
            var declared = DeclaredValidators.Collect(recordTypes, new HashSet<Type>(recordTypes), configurationResult);

            return new ValidationExecutor(recordTypes, recordGetter, declared, validators, configurationResult);
        }

        /// <summary>結線済みテーブル（レコード型と資産の対。validator の発見や検証は行わない）。</summary>
        public static IReadOnlyList<(Type RecordType, ScriptableTableBase Table)> WiredTables(ScriptableObject database) =>
            ScriptableDatabaseSchema.WiredTables(database);

        /// <summary>結線済みテーブルのレコード型（一覧表示用）。</summary>
        public static IReadOnlyList<Type> WiredRecordTypes(ScriptableObject database) =>
            WiredTables(database).Select(x => x.RecordType).ToList();

        private ValidationExecutor(
            IReadOnlyList<Type> recordTypes,
            IRecordGetter recordGetter,
            IEnumerable<object> declaredValidators,
            IEnumerable<object> validators,
            ValidationResult configurationResult)
        {
            RecordTypes = recordTypes;
            _recordGetter = recordGetter ?? throw new ArgumentNullException(nameof(recordGetter));
            ConfigurationResult = configurationResult;

            var tableValidators = RegisterValidators(
                declaredValidators.Concat(validators ?? Enumerable.Empty<object>()));
            _tableValidators = BuildTableValidators(tableValidators);
        }

        /// <summary>構成チェックと全テーブルの検証を実行する。</summary>
        public IReadOnlyList<ValidationResult> ExecuteAll()
        {
            var results = new List<ValidationResult>(_tableValidators.Count + 1) { ConfigurationResult };
            foreach (var tableValidator in _tableValidators)
            {
                results.Add(ExecuteTimed(tableValidator));
            }

            return results;
        }

        /// <summary>単一テーブルの検証を実行する。</summary>
        public ValidationResult Execute(Type recordType)
        {
            var tableValidator = _tableValidators.FirstOrDefault(x => x.RecordType == recordType);
            if (tableValidator == null)
                throw new ArgumentException($"{recordType?.Name ?? "null"} は検証対象のテーブルではありません。", nameof(recordType));

            return ExecuteTimed(tableValidator);
        }

        public ValidationResult Execute<TRecord>() => Execute(typeof(TRecord));

        private ValidationResult ExecuteTimed(ITableValidator tableValidator)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = (ValidationResult)_executeCoreMethod
                .MakeGenericMethod(tableValidator.RecordType)
                .Invoke(this, new object[] { tableValidator });
            result.CheckTime = stopwatch.Elapsed;
            return result;
        }

        private ValidationResult ExecuteCore<TRecord>(ITableValidator<TRecord> tableValidator)
        {
            try
            {
                return tableValidator.ValidateAll(
                    Registered<IRecordValidator<TRecord>>(_recordValidators, typeof(TRecord)),
                    Registered<IRecordsValidator<TRecord>>(_recordsValidators, typeof(TRecord)),
                    _recordGetter);
            }
            catch (Exception e)
            {
                // RecordCount = -1 は「検証が中断した」ことを表す。原因追跡のため例外は全容を残す。
                var result = new ValidationResult(typeof(TRecord).Name, -1);
                result.AddError(typeof(TRecord).Name, e.ToString());
                return result;
            }
        }

        private static IReadOnlyList<TValidator> Registered<TValidator>(Dictionary<Type, IList> map, Type recordType) =>
            map.TryGetValue(recordType, out var list) ? (IReadOnlyList<TValidator>)list : Array.Empty<TValidator>();

        // ---- 登録 ----

        private Dictionary<Type, ITableValidator> RegisterValidators(IEnumerable<object> validators)
        {
            var targets = new HashSet<Type>(RecordTypes);
            var tableValidators = new Dictionary<Type, ITableValidator>();

            foreach (var validator in validators)
            {
                if (validator == null) continue;

                if (validator is ITableValidator tableValidator)
                {
                    if (!IsTarget(targets, tableValidator.RecordType, "TableValidator")) continue;

                    if (tableValidators.TryGetValue(tableValidator.RecordType, out var registered))
                    {
                        ConfigurationResult.AddError(tableValidator.RecordType.Name,
                            $"TableValidator が複数あります（{registered.GetType().Name} / {validator.GetType().Name}）。");
                        continue;
                    }

                    tableValidators.Add(tableValidator.RecordType, tableValidator);
                    continue;
                }

                Register(_recordValidators, typeof(IRecordValidator<>), validator, targets, "RecordValidator");
                Register(_recordsValidators, typeof(IRecordsValidator<>), validator, targets, "TableRecordsValidator");
            }

            return tableValidators;
        }

        private void Register(Dictionary<Type, IList> map, Type openInterface, object validator, HashSet<Type> targets, string kind)
        {
            foreach (var recordType in Targets(validator.GetType(), openInterface))
            {
                if (!IsTarget(targets, recordType, kind)) continue;

                if (!map.TryGetValue(recordType, out var list))
                {
                    var listType = typeof(List<>).MakeGenericType(openInterface.MakeGenericType(recordType));
                    list = (IList)Activator.CreateInstance(listType);
                    map.Add(recordType, list);
                }

                list.Add(validator);
            }
        }

        // 検証対象のテーブルが無い validator は登録しない（BuildTableValidators / ExecuteCore のどちらからも
        // 参照されず、実行されることがないため）。ただしマスターデータのレコード型（[ScriptableTable]）に
        // 対するものは実行されないまま消えるのを顕在化させる。テーブル化されていない型は本機構の管轄外。
        private bool IsTarget(HashSet<Type> targets, Type recordType, string kind)
        {
            if (targets.Contains(recordType)) return true;

            if (recordType.GetCustomAttribute<ScriptableTableAttribute>() != null && _reportedOrphans.Add((recordType, kind)))
                ConfigurationResult.AddError(recordType.Name, $"この型の {kind} がありますが、検証対象のテーブルがありません。");

            return false;
        }

        // validator が無いテーブルも件数付きで結果に出すため、全テーブルに既定 TableValidator を補完する。
        private List<ITableValidator> BuildTableValidators(Dictionary<Type, ITableValidator> tableValidators)
        {
            var list = new List<ITableValidator>(RecordTypes.Count);
            foreach (var recordType in RecordTypes)
            {
                list.Add(tableValidators.TryGetValue(recordType, out var tableValidator)
                    ? tableValidator
                    : (ITableValidator)Activator.CreateInstance(typeof(TableValidator<>).MakeGenericType(recordType)));
            }

            return list;
        }

        // ---- 発見 ----

        private static IEnumerable<object> DiscoverValidators(ValidationResult configurationResult)
        {
            foreach (var type in ValidationReflection.AllTypes())
            {
                if (!type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition) continue;

                bool isTableValidator = typeof(ITableValidator).IsAssignableFrom(type);
                bool isRecordValidator = Targets(type, typeof(IRecordValidator<>)).Any()
                    || Targets(type, typeof(IRecordsValidator<>)).Any();
                if (!isTableValidator && !isRecordValidator) continue;

                if (isTableValidator && !DerivesFromTableValidator(type))
                {
                    configurationResult.AddError(type.Name,
                        "ITableValidator<T> を直接実装しています。RecordValidator の実行契約を満たすため TableValidator<T> を継承してください。");
                    continue;
                }

                object instance = null;
                try
                {
                    instance = Activator.CreateInstance(type);
                }
                catch (Exception e)
                {
                    configurationResult.AddError(type.Name, $"validator を生成できません（引数なしコンストラクタが必要）: {e.Message}");
                }

                if (instance != null) yield return instance;
            }
        }

        private static IEnumerable<Type> Targets(Type type, Type openInterface) =>
            type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openInterface)
                .Select(i => i.GetGenericArguments()[0]);

        private static bool DerivesFromTableValidator(Type type) =>
            ValidationReflection.TryGetGenericBaseArguments(type, typeof(TableValidator<>), out _);
    }
}
#endif
