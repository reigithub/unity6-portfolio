using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Core.MasterData.MemoryTables;
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
                        var value = ConvertToType(values[index], property.PropertyType);
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

        public static object ConvertToType(object fromValue, Type type)
        {
            var converter = TypeDescriptor.GetConverter(type);
            if (converter.CanConvertFrom(fromValue.GetType()))
            {
                return converter.ConvertFrom(fromValue);
            }

            throw new InvalidOperationException("変換エラー");
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