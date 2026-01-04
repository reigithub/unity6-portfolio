using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Core.MasterData.MemoryTables;
using MasterMemory;
using MessagePack;
using MessagePack.Resolvers;
using UnityEngine;

namespace Game.Core.MasterData
{
    public static class MasterDataHelper
    {
        private const string TsvPathFormat = "Assets/MasterData/Tsv/{0}.tsv";
        private const string TsvTempPathFormat = "Assets/MasterData/Tsv/{0}.temp.tsv";
        private const string TsvBackupPathFormat = "Assets/MasterData/Tsv/{0}.bak.tsv";
        private const char TsvColumnSeparator = '\t';

        private const string MasterDataBinaryPath = "Assets/MasterData/MasterDataBinary.bytes";
        private const string MasterDataBinaryTempPath = "Assets/MasterData/MasterDataBinary.temp.bytes";
        private const string MasterDataBinaryBackupPath = "Assets/MasterData/MasterDataBinary.bak.bytes";

        public static Type[] GetMemoryTableTypes()
        {
            Assembly assembly = Assembly.GetAssembly(typeof(StageMaster));
            // Assembly assembly = Assembly.GetExecutingAssembly();

            Type[] types = assembly.GetTypes();
            var memoryTables = new List<Type>();
            foreach (Type type in types)
            {
                if (!type.IsClass)
                    continue;

                if (IsMemoryTable(type))
                {
                    memoryTables.Add(type);
                }
            }

            return memoryTables.ToArray();
        }

        /// <summary>
        /// Tsvを作成
        /// </summary>
        public static void GenerateTsv(Type memoryTableType, bool replace = false, bool backup = false)
        {
            if (!IsMemoryTable(memoryTableType))
                return;

            if (File.Exists(GetTsvPath(memoryTableType.Name)))
            {
                ReplaceTsv(memoryTableType, replace, backup);
            }
            else
            {
                CreateTsv(memoryTableType);
            }
        }

        /// <summary>
        /// すべてのMemoryTableのTsvを作成
        /// </summary>
        public static void GenerateTsvAll(bool replace = false, bool backup = false)
        {
            var memoryTableTypes = GetMemoryTableTypes();

            foreach (var memoryTableType in memoryTableTypes)
            {
                GenerateTsv(memoryTableType, replace, backup);
            }
        }

        /// <summary>
        /// マスタデータバイナリを作成
        /// </summary>
        public static void GenerateMasterDataBinary()
        {
            if (!TryCreateDirectoryIfNotExists(MasterDataBinaryPath))
                return;

            var memoryTableTypes = GetMemoryTableTypes();
            if (memoryTableTypes.Length == 0)
                return;

            // DatabaseBuilderを使ってバイナリデータを生成する
            var formatterResolver = CompositeResolver.Create(GetMessagePackFormatterResolvers());
            var databaseBuilder = new DatabaseBuilder(formatterResolver);

            var appendMethods = databaseBuilder
                .GetType()
                .GetMethods()
                .Where(x => x.Name == "Append" && x.GetParameters().Length == 1)
                .ToDictionary(x => x.GetParameters().First().ParameterType.GetGenericArguments().First());

            foreach (var memoryTableType in memoryTableTypes)
            {
                object[] elements;
                try
                {
                    elements = ReadTsv(memoryTableType).ToArray();
                }
                catch (Exception e)
                {
                    Debug.LogError($"Tsv Read Error. TableName: {memoryTableType.Name}\n{e}");
                    continue;
                }

                var masterElements = Array.CreateInstance(memoryTableType, elements.Length);
                Array.Copy(elements, masterElements, elements.Length);

                // Memo: アペンドメソッドを実行すると、ビルダーのバッファに書き込まれる
                if (appendMethods.TryGetValue(memoryTableType, out var appendMethod))
                {
                    appendMethod.Invoke(databaseBuilder, new object[] { masterElements });
                }

                // databaseBuilder.AppendDynamic(memoryTableType, elements);
            }

            // ビルダーのバッファからバイナリをビルド
            var binary = databaseBuilder.Build();

            // バイナリを永続化
            File.WriteAllBytes(MasterDataBinaryTempPath, binary);

            // atomic
            File.Replace(MasterDataBinaryTempPath, MasterDataBinaryPath, MasterDataBinaryBackupPath);
        }

#if UNITY_EDITOR
        /// <summary>
        /// マスタデータバイナリを読込
        /// </summary>
        public static MemoryDatabase LoadMasterDataBinary()
        {
            // バイナリをロード
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(MasterDataBinaryPath);
            var binary = asset.bytes;

            // バイナリからMemoryDatabaseを作成
            var formatterResolver = CompositeResolver.Create(GetMessagePackFormatterResolvers());
            var memoryDatabase = new MemoryDatabase(binary, formatterResolver: formatterResolver, maxDegreeOfParallelism: Environment.ProcessorCount);
            return memoryDatabase;
        }
#endif

        private static void ReplaceTsv(Type memoryTableType, bool replace = true, bool backup = false)
        {
            if (!replace) return;

            string memoryTableName = memoryTableType.Name;
            string tsvPath = GetTsvPath(memoryTableName);

            if (!File.Exists(tsvPath)) return;

            var properties = GetMemoryTableProperties(memoryTableType);

            var instances = ReadTsv(memoryTableType).ToArray();

            var tempPath = GetTsvTempPath(memoryTableName);

            string backupPath = backup ? GetTsvBackupPath(memoryTableName) : null;

            WriteTsv(tempPath, instances, properties);

            // atomic
            File.Replace(tempPath, tsvPath, backupPath);
        }

        private static void CreateTsv(Type memoryTableType)
        {
            string memoryTableName = memoryTableType.Name;
            string tsvPath = GetTsvPath(memoryTableName);

            if (!TryCreateDirectoryIfNotExists(tsvPath))
                return;

            // 新規作成時は適当なデータを数行詰め込む
            var properties = GetMemoryTableProperties(memoryTableType);
            var instances = new List<object>();
            for (int id = 1; id <= 3; id++)
            {
                var instance = Activator.CreateInstance(memoryTableType);

                foreach (var property in properties)
                {
                    if (IsMemoryTablePrimaryKey(property))
                    {
                        property.SetValue(instance, id);
                        continue;
                    }

                    property.SetValue(instance, null); // nullを設定すると値型は規定値(default)になる
                }

                instances.Add(instance);
            }

            WriteTsv(tsvPath, instances.ToArray(), properties);
        }

        private static void WriteTsv(string tsvPath, object[] instances, PropertyInfo[] properties)
        {
            using (StreamWriter writer = new StreamWriter(tsvPath))
            {
                var columnNames = properties.Select(x => x.Name).ToArray();
                var columnNameLine = string.Join(TsvColumnSeparator, columnNames);
                writer.WriteLine(columnNameLine);

                foreach (var instance in instances)
                {
                    var row = properties.Select(x => x.GetValue(instance)?.ToString()).ToArray();
                    var line = string.Join(TsvColumnSeparator, row);
                    writer.WriteLine(line);
                }
            }
        }

        private static IEnumerable<object> ReadTsv(Type memoryTableType)
        {
            string tsvPath = GetTsvPath(memoryTableType.Name);
            if (!File.Exists(tsvPath))
                yield break;

            var lines = File.ReadAllLines(tsvPath);
            var columnNames = lines
                .First()
                .Split(TsvColumnSeparator)
                .Select((name, index) => (name, index))
                .ToDictionary(x => x.name, x => x.index);
            var properties = GetMemoryTableProperties(memoryTableType);
            // var newColumnIndexDict = properties
            //     .Select((property, columnIndex) => (columnName: property.Name, columnIndex))
            //     .ToDictionary(x => x.columnName, x => x.columnIndex);

            foreach (var line in lines.Skip(1))
            {
                var values = line.Split(TsvColumnSeparator);
                var instance = Activator.CreateInstance(memoryTableType);

                foreach (var property in properties)
                {
                    if (columnNames.TryGetValue(property.Name, out var index))
                    {
                        var value = ParseValue(property.PropertyType, values[index]);
                        property.SetValue(instance, value);
                    }
                    else
                    {
                        property.SetValue(instance, null);
                    }
                }

                yield return instance;
            }
        }

        private static string GetTsvPath(string memoryTableName)
        {
            return string.Format(TsvPathFormat, memoryTableName);
        }

        private static string GetTsvTempPath(string memoryTableName)
        {
            return string.Format(TsvTempPathFormat, memoryTableName);
        }

        private static string GetTsvBackupPath(string memoryTableName)
        {
            return string.Format(TsvBackupPathFormat, memoryTableName);
        }

        public static bool TryCreateDirectoryIfNotExists(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (directory == null)
                return false;

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return true;
        }

        private static bool IsMemoryTable(Type type)
        {
            return type.GetCustomAttribute(typeof(MasterMemory.MemoryTableAttribute)) != null;
        }

        private static bool IsMemoryTablePrimaryKey(PropertyInfo property)
        {
            return property.GetCustomAttribute(typeof(MasterMemory.PrimaryKeyAttribute)) != null;
        }

        private static PropertyInfo[] GetMemoryTableProperties(Type type)
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        }

        public static object ConvertToType(Type type, object value)
        {
            var converter = TypeDescriptor.GetConverter(type);
            if (converter.CanConvertFrom(value.GetType()))
            {
                return converter.ConvertFrom(value);
            }

            throw new InvalidOperationException("変換エラー");
        }

        // https://github.com/Cysharp/MasterMemory#metadata
        public static object ParseValue(Type type, string rawValue)
        {
            if (type == typeof(string)) return rawValue;

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                if (string.IsNullOrWhiteSpace(rawValue)) return null;
                return ParseValue(type.GenericTypeArguments[0], rawValue);
            }

            if (type.IsEnum)
            {
                // ここで基底型(intなど)に変換できたとしても、Enum型でPropertyInfo.SetValueされてしまうので意味なし
                // https://github.com/Cysharp/MasterMemory/issues/102
                var value = Enum.Parse(type, rawValue);
                var underlyingType = Enum.GetUnderlyingType(type);
                var convertValue = Convert.ChangeType(value, underlyingType);
                return convertValue;
            }

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                    // True/False or 0,1
                    if (int.TryParse(rawValue, out var intBool))
                    {
                        return Convert.ToBoolean(intBool);
                    }

                    return Boolean.Parse(rawValue);
                case TypeCode.Char:
                    return Char.Parse(rawValue);
                case TypeCode.SByte:
                    return SByte.Parse(rawValue, CultureInfo.InvariantCulture);
                case TypeCode.Byte:
                    return Byte.Parse(rawValue, CultureInfo.InvariantCulture);
                case TypeCode.Int16:
                    return Int16.Parse(rawValue, CultureInfo.InvariantCulture);
                case TypeCode.UInt16:
                    return UInt16.Parse(rawValue, CultureInfo.InvariantCulture);
                case TypeCode.Int32:
                    return Int32.Parse(rawValue, CultureInfo.InvariantCulture);
                case TypeCode.UInt32:
                    return UInt32.Parse(rawValue, CultureInfo.InvariantCulture);
                case TypeCode.Int64:
                    return Int64.Parse(rawValue, CultureInfo.InvariantCulture);
                case TypeCode.UInt64:
                    return UInt64.Parse(rawValue, CultureInfo.InvariantCulture);
                case TypeCode.Single:
                    return Single.Parse(rawValue, CultureInfo.InvariantCulture);
                case TypeCode.Double:
                    return Double.Parse(rawValue, CultureInfo.InvariantCulture);
                case TypeCode.Decimal:
                    return Decimal.Parse(rawValue, CultureInfo.InvariantCulture);
                case TypeCode.DateTime:
                    return DateTime.Parse(rawValue, CultureInfo.InvariantCulture);
                default:
                    if (type == typeof(DateTimeOffset))
                    {
                        return DateTimeOffset.Parse(rawValue, CultureInfo.InvariantCulture);
                    }
                    else if (type == typeof(TimeSpan))
                    {
                        return TimeSpan.Parse(rawValue, CultureInfo.InvariantCulture);
                    }
                    else if (type == typeof(Guid))
                    {
                        return Guid.Parse(rawValue);
                    }

                    // or other your custom parsing.
                    throw new NotSupportedException();
            }
        }

        public static IFormatterResolver[] GetMessagePackFormatterResolvers()
        {
            return new[]
            {
                MasterMemoryResolver.Instance, // 自動生成されたResolver
                StandardResolver.Instance      // MessagePackの標準Resolver
            };
        }
    }
}