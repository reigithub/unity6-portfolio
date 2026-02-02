using System.Globalization;
using System.Reflection;

namespace Game.Tools.Data;

/// <summary>
/// Reads TSV files and converts rows to typed objects using reflection.
/// </summary>
public static class TsvReader
{
    private const char ColumnSeparator = '\t';

    /// <summary>
    /// Read a TSV file and return typed instances.
    /// </summary>
    public static object[] ReadTsv(Type type, string tsvPath)
    {
        if (!File.Exists(tsvPath))
        {
            return [];
        }

        var lines = File.ReadAllLines(tsvPath);
        if (lines.Length < 2)
        {
            return [];
        }

        var columnNames = lines[0]
            .Split(ColumnSeparator)
            .Select((name, index) => (name, index))
            .ToDictionary(x => x.name, x => x.index);

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var result = new List<object>();

        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var values = line.Split(ColumnSeparator);
            var instance = Activator.CreateInstance(type)!;

            foreach (var property in properties)
            {
                if (!columnNames.TryGetValue(property.Name, out var index))
                {
                    continue;
                }

                if (index >= values.Length)
                {
                    continue;
                }

                var value = ParseValue(property.PropertyType, values[index]);
                property.SetValue(instance, value);
            }

            result.Add(instance);
        }

        return result.ToArray();
    }

    /// <summary>
    /// Read the header row of a TSV file and return column names.
    /// </summary>
    public static string[] ReadHeaders(string tsvPath)
    {
        if (!File.Exists(tsvPath))
        {
            return [];
        }

        using var reader = new StreamReader(tsvPath);
        var headerLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            return [];
        }

        return headerLine.Split(ColumnSeparator);
    }

    /// <summary>
    /// Read a TSV file as raw strings (no type conversion).
    /// Returns headers and all data rows.
    /// </summary>
    public static (string[] Headers, string[][] Rows) ReadTsvRaw(string tsvPath)
    {
        if (!File.Exists(tsvPath))
        {
            return ([], []);
        }

        var lines = File.ReadAllLines(tsvPath);
        if (lines.Length < 2)
        {
            return (lines.Length == 1 ? lines[0].Split(ColumnSeparator) : [], []);
        }

        var headers = lines[0].Split(ColumnSeparator);
        var rows = lines.Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Split(ColumnSeparator))
            .ToArray();

        return (headers, rows);
    }

    /// <summary>
    /// Parse a raw string value based on PostgreSQL udt_name.
    /// When the column is NOT NULL, empty strings are preserved for text types
    /// and default values are used for numeric/other types.
    /// </summary>
    public static object? ParseValueByUdtName(string udtName, string rawValue, bool isNullable)
    {
        if (string.IsNullOrEmpty(rawValue))
        {
            if (isNullable)
            {
                return null;
            }

            // NOT NULL column: return type-appropriate default
            return udtName switch
            {
                "varchar" or "text" or "bpchar" => string.Empty,
                _ => null,
            };
        }

        return udtName switch
        {
            "int4" => int.Parse(rawValue, CultureInfo.InvariantCulture),
            "int2" => short.Parse(rawValue, CultureInfo.InvariantCulture),
            "int8" => long.Parse(rawValue, CultureInfo.InvariantCulture),
            "float4" => float.Parse(rawValue, CultureInfo.InvariantCulture),
            "float8" => double.Parse(rawValue, CultureInfo.InvariantCulture),
            "numeric" => decimal.Parse(rawValue, CultureInfo.InvariantCulture),
            "varchar" or "text" or "bpchar" => rawValue,
            "bool" => rawValue is "1" or "true" or "True" or "TRUE",
            "timestamp" => DateTime.Parse(rawValue, CultureInfo.InvariantCulture),
            "timestamptz" => DateTimeOffset.Parse(rawValue, CultureInfo.InvariantCulture),
            "uuid" => Guid.Parse(rawValue),
            _ => rawValue,
        };
    }

    /// <summary>
    /// Parse a string value into the target type.
    /// Mirrors the logic in MasterDataHelper.ParseValue from the Unity client.
    /// </summary>
    public static object? ParseValue(Type type, string rawValue)
    {
        if (type == typeof(string))
        {
            return rawValue;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return null;
            }

            return ParseValue(type.GenericTypeArguments[0], rawValue);
        }

        if (type.IsEnum)
        {
            var value = Enum.Parse(type, rawValue);
            var underlyingType = Enum.GetUnderlyingType(type);
            return Convert.ChangeType(value, underlyingType);
        }

        return Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean when int.TryParse(rawValue, out var intBool) => Convert.ToBoolean(intBool),
            TypeCode.Boolean => bool.Parse(rawValue),
            TypeCode.Char => char.Parse(rawValue),
            TypeCode.SByte => sbyte.Parse(rawValue, CultureInfo.InvariantCulture),
            TypeCode.Byte => byte.Parse(rawValue, CultureInfo.InvariantCulture),
            TypeCode.Int16 => short.Parse(rawValue, CultureInfo.InvariantCulture),
            TypeCode.UInt16 => ushort.Parse(rawValue, CultureInfo.InvariantCulture),
            TypeCode.Int32 => int.Parse(rawValue, CultureInfo.InvariantCulture),
            TypeCode.UInt32 => uint.Parse(rawValue, CultureInfo.InvariantCulture),
            TypeCode.Int64 => long.Parse(rawValue, CultureInfo.InvariantCulture),
            TypeCode.UInt64 => ulong.Parse(rawValue, CultureInfo.InvariantCulture),
            TypeCode.Single => float.Parse(rawValue, CultureInfo.InvariantCulture),
            TypeCode.Double => double.Parse(rawValue, CultureInfo.InvariantCulture),
            TypeCode.Decimal => decimal.Parse(rawValue, CultureInfo.InvariantCulture),
            TypeCode.DateTime => DateTime.Parse(rawValue, CultureInfo.InvariantCulture),
            _ when type == typeof(DateTimeOffset) => DateTimeOffset.Parse(rawValue, CultureInfo.InvariantCulture),
            _ when type == typeof(TimeSpan) => TimeSpan.Parse(rawValue, CultureInfo.InvariantCulture),
            _ when type == typeof(Guid) => Guid.Parse(rawValue),
            _ => throw new NotSupportedException($"Unsupported type: {type.FullName}"),
        };
    }
}
