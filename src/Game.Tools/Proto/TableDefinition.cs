namespace Game.Tools.Proto;

/// <summary>
/// Represents a parsed proto message mapped to a MasterMemory table.
/// </summary>
public record TableDefinition
{
    public required string TableName { get; init; }
    public required string ProtoFile { get; init; }
    public int DeployTarget { get; init; }
    public required List<FieldDefinition> Fields { get; init; }
    public List<SecondaryKeyDefinition> SecondaryKeys { get; init; } = [];
}

/// <summary>
/// Represents a field within a table definition.
/// </summary>
public record FieldDefinition
{
    public required string ProtoName { get; init; }
    public required string CSharpName { get; init; }
    public required string CSharpType { get; init; }
    public required string ProtoType { get; init; }
    public bool IsOptional { get; init; }
    public int DeployTarget { get; init; }
    public int FieldNumber { get; init; }

    // Primary key
    public bool IsPrimaryKey { get; init; }
    public int PrimaryKeyOrder { get; init; }

    // Secondary key (single, direct on the field)
    public List<SecondaryKeyInfo> SecondaryKeys { get; init; } = [];
}

/// <summary>
/// Secondary key info attached directly to a field via proto field options.
/// </summary>
public record SecondaryKeyInfo
{
    public int Index { get; init; }
    public int KeyOrder { get; init; }
    public bool NonUnique { get; init; }
}

/// <summary>
/// Secondary key definition from message-level options (for complex multi-field scenarios).
/// </summary>
public record SecondaryKeyDefinition
{
    public required string FieldName { get; init; }
    public int SecondaryIndex { get; init; }
    public int KeyOrder { get; init; }
    public bool NonUnique { get; init; }
}
