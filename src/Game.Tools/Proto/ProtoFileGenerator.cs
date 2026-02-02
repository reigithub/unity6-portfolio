using System.Reflection;
using System.Text;
using Game.Tools.CodeGen;
using MasterMemory;

namespace Game.Tools.Proto;

/// <summary>
/// Generates .proto files from C# MemoryTable classes (reverse scaffolding).
/// </summary>
public class ProtoFileGenerator
{
    private static readonly Dictionary<Type, string> CSharpToProtoTypeMap = new()
    {
        [typeof(int)] = "int32",
        [typeof(long)] = "int64",
        [typeof(uint)] = "uint32",
        [typeof(ulong)] = "uint64",
        [typeof(float)] = "float",
        [typeof(double)] = "double",
        [typeof(bool)] = "bool",
        [typeof(string)] = "string",
        [typeof(byte[])] = "bytes",
    };

    /// <summary>
    /// Generate a .proto file text from a MemoryTable-attributed C# type.
    /// </summary>
    public string Generate(Type type, string subDir)
    {
        var sb = new StringBuilder();
        var pascalSubDir = NameConverter.ToPascalCase(subDir.TrimEnd('/'));

        sb.AppendLine("syntax = \"proto3\";");
        sb.AppendLine($"package masterdata.{subDir.TrimEnd('/')};");
        sb.AppendLine("import \"options/masterdata_options.proto\";");
        sb.AppendLine($"option csharp_namespace = \"Game.Tools.Proto.{pascalSubDir}\";");
        sb.AppendLine();
        sb.AppendLine($"message {type.Name} {{");
        sb.AppendLine("  option (masterdata.options.table_target) = DEPLOY_TARGET_ALL;");

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .ToArray();

        bool isCompositeKey = HasCompositePrimaryKey(type);

        // Collect message-level secondary_keys for multi-index fields
        var messageSecondaryKeys = CollectMessageLevelSecondaryKeys(properties);
        foreach (var sk in messageSecondaryKeys)
        {
            sb.AppendLine($"  option (masterdata.options.secondary_keys) = {{field_name: \"{sk.FieldName}\", secondary_index: {sk.SecondaryIndex}, key_order: {sk.KeyOrder}, non_unique: {sk.NonUnique.ToString().ToLowerInvariant()}}};");
        }

        sb.AppendLine();

        int fieldNumber = 1;
        foreach (var prop in properties)
        {
            var line = BuildFieldLine(prop, fieldNumber, isCompositeKey, messageSecondaryKeys);
            sb.AppendLine($"  {line}");
            fieldNumber++;
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Estimate the best-matching subdirectory for a class name based on existing directories.
    /// </summary>
    public static string EstimateSubDirectory(string className, string outDir)
    {
        var snakeName = NameConverter.ToSnakeCase(className);
        var parts = snakeName.Split('_');

        if (!Directory.Exists(outDir))
        {
            return parts[0];
        }

        var existingDirs = Directory.GetDirectories(outDir)
            .Select(d => Path.GetFileName(d))
            .Where(d => d != "options")
            .ToArray();

        if (existingDirs.Length == 0)
        {
            return parts[0];
        }

        // Longest prefix match against existing directory names
        string bestMatch = parts[0];
        int bestLength = 0;

        foreach (var dir in existingDirs)
        {
            var dirSnake = dir.Replace('-', '_');
            if (snakeName.StartsWith(dirSnake + "_", StringComparison.Ordinal) && dirSnake.Length > bestLength)
            {
                bestLength = dirSnake.Length;
                bestMatch = dir;
            }
        }

        return bestMatch;
    }

    private static string BuildFieldLine(PropertyInfo prop, int fieldNumber, bool isCompositeKey, List<MessageSecondaryKey> messageLevelKeys)
    {
        var propType = prop.PropertyType;
        bool isOptional = false;
        string protoType;

        // Handle Nullable<T>
        var underlyingType = Nullable.GetUnderlyingType(propType);
        if (underlyingType != null)
        {
            isOptional = true;
            propType = underlyingType;
        }

        if (!CSharpToProtoTypeMap.TryGetValue(propType, out protoType!))
        {
            protoType = "int32";
        }

        var fieldName = NameConverter.ToSnakeCase(prop.Name);
        var options = BuildFieldOptions(prop, isCompositeKey, fieldName, messageLevelKeys);

        var prefix = isOptional ? "optional " : string.Empty;
        var optionsSuffix = options.Length > 0 ? $" [{options}]" : string.Empty;

        return $"{prefix}{protoType} {fieldName} = {fieldNumber}{optionsSuffix};";
    }

    private static string BuildFieldOptions(PropertyInfo prop, bool isCompositeKey, string snakeFieldName, List<MessageSecondaryKey> messageLevelKeys)
    {
        var parts = new List<string>();

        var primaryKey = prop.GetCustomAttribute<PrimaryKeyAttribute>();
        if (primaryKey != null)
        {
            parts.Add("(masterdata.options.index_type) = INDEX_PRIMARY");
            if (isCompositeKey)
            {
                var keyOrder = primaryKey.KeyOrder;
                parts.Add($"(masterdata.options.primary_key_order) = {keyOrder}");
            }
        }

        // Get all SecondaryKey attributes on this field
        var secondaryKeys = prop.GetCustomAttributes<SecondaryKeyAttribute>().ToArray();
        if (secondaryKeys.Length > 0 && primaryKey == null)
        {
            // Emit the first SecondaryKey at field-level
            var sk = secondaryKeys[0];

            // Check if this first SK is also in message-level keys (meaning it was promoted)
            // In that case, don't emit at field-level — only emit if it's a genuine field-level SK
            bool isFieldLevel = !messageLevelKeys.Any(m =>
                m.FieldName == snakeFieldName && m.SecondaryIndex == sk.IndexNo && m.KeyOrder == sk.KeyOrder);

            if (isFieldLevel)
            {
                parts.Add("(masterdata.options.index_type) = INDEX_SECONDARY");
                parts.Add($"(masterdata.options.secondary_index) = {sk.IndexNo}");

                if (sk.KeyOrder > 0)
                {
                    parts.Add($"(masterdata.options.key_order) = {sk.KeyOrder}");
                }

                var nonUnique = prop.GetCustomAttribute<NonUniqueAttribute>();
                if (nonUnique != null)
                {
                    parts.Add("(masterdata.options.non_unique) = true");
                }
            }
        }

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Collect secondary keys that need message-level definitions.
    /// Fields with multiple SecondaryKey attributes: the first goes on the field,
    /// additional ones go to message-level secondary_keys options.
    /// </summary>
    private static List<MessageSecondaryKey> CollectMessageLevelSecondaryKeys(PropertyInfo[] properties)
    {
        var result = new List<MessageSecondaryKey>();

        foreach (var prop in properties)
        {
            var secondaryKeys = prop.GetCustomAttributes<SecondaryKeyAttribute>().ToArray();
            if (secondaryKeys.Length <= 1)
            {
                continue;
            }

            // Skip the first one (it's handled at field-level), emit the rest at message-level.
            // NonUnique attribute applies only to the first SecondaryKey (field-level),
            // not to additional message-level secondary keys.
            foreach (var sk in secondaryKeys.Skip(1))
            {
                result.Add(new MessageSecondaryKey
                {
                    FieldName = NameConverter.ToSnakeCase(prop.Name),
                    SecondaryIndex = sk.IndexNo,
                    KeyOrder = sk.KeyOrder,
                    NonUnique = false,
                });
            }
        }

        // Also collect fields that participate in a composite secondary key
        // but only via message-level (they have one SecondaryKey with keyOrder > 0
        // and no field-level secondary designation, because the field line has no options)
        // Example: SurvivorWeaponLevelMaster.Level has [SecondaryKey(1, keyOrder: 1)]
        // but no field-level INDEX_SECONDARY — it's purely message-level

        // Find all secondary key indices that have multi-field composite keys
        var allSecondaryEntries = new List<(string FieldName, int IndexNo, int KeyOrder)>();
        foreach (var prop in properties)
        {
            foreach (var sk in prop.GetCustomAttributes<SecondaryKeyAttribute>())
            {
                allSecondaryEntries.Add((
                    NameConverter.ToSnakeCase(prop.Name),
                    sk.IndexNo,
                    sk.KeyOrder));
            }
        }

        // Group by index, find composite ones (multiple fields in same index)
        var compositeIndices = allSecondaryEntries
            .GroupBy(e => e.IndexNo)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet();

        // For composite indices, any field member that doesn't already have field-level
        // representation should be added to message-level
        foreach (var prop in properties)
        {
            var sks = prop.GetCustomAttributes<SecondaryKeyAttribute>().ToArray();
            if (sks.Length != 1)
            {
                continue; // already handled above (multi-SK) or no SK
            }

            var sk = sks[0];
            if (!compositeIndices.Contains(sk.IndexNo))
            {
                continue;
            }

            var fieldName = NameConverter.ToSnakeCase(prop.Name);
            // Check: if this field also has a different first SK at field-level,
            // this SK is already in message-level via the multi-SK path
            // Here we handle the case where the field has ONLY one SK that is part of a composite
            // For the first field in the composite (field-level), we DON'T add to message-level
            // For subsequent fields (keyOrder > 0 or the field has no other INDEX_SECONDARY at field-level)

            // Check if this field already has field-level INDEX_SECONDARY for a different index
            bool hasFieldLevelSecondary = sks.Length == 1 &&
                allSecondaryEntries.Count(e => e.FieldName == fieldName) == 1;

            if (hasFieldLevelSecondary && !result.Any(r => r.FieldName == fieldName && r.SecondaryIndex == sk.IndexNo))
            {
                result.Add(new MessageSecondaryKey
                {
                    FieldName = fieldName,
                    SecondaryIndex = sk.IndexNo,
                    KeyOrder = sk.KeyOrder,
                    NonUnique = prop.GetCustomAttribute<NonUniqueAttribute>() != null,
                });
            }
        }

        // Sort by secondary_index then key_order for consistent output
        return result.OrderBy(k => k.SecondaryIndex).ThenBy(k => k.KeyOrder).ToList();
    }

    /// <summary>
    /// Check if the type has composite primary keys (multiple fields with PrimaryKey).
    /// </summary>
    public static bool HasCompositePrimaryKey(Type type)
    {
        var count = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Count(p => p.GetCustomAttribute<PrimaryKeyAttribute>() != null);
        return count > 1;
    }

    private record MessageSecondaryKey
    {
        public required string FieldName { get; init; }
        public int SecondaryIndex { get; init; }
        public int KeyOrder { get; init; }
        public bool NonUnique { get; init; }
    }
}
