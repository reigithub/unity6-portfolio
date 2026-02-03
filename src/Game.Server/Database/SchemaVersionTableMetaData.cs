using FluentMigrator.Runner.VersionTableInfo;

namespace Game.Server.Database;

public class SchemaVersionTableMetaData : IVersionTableMetaData
{
    public SchemaVersionTableMetaData(string schemaName)
    {
        SchemaName = schemaName;
    }

    public object? ApplicationContext { get; set; }
    public bool OwnsSchema => true;
    public string SchemaName { get; }
    public string TableName => "VersionInfo";
    public string ColumnName => "Version";
    public string DescriptionColumnName => "Description";
    public string UniqueIndexName => $"UC_{SchemaName}_Version";
    public string AppliedOnColumnName => "AppliedOn";
    public bool CreateWithPrimaryKey => false;
}
