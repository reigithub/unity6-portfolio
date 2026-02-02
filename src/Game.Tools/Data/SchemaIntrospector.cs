using Dapper;
using Npgsql;

namespace Game.Tools.Data;

public record ColumnInfo(
    string ColumnName,
    int OrdinalPosition,
    string UdtName,
    bool IsNullable,
    bool IsIdentity);

public record TableSchema(
    string SchemaName,
    string TableName,
    ColumnInfo[] Columns);

/// <summary>
/// Queries PostgreSQL information_schema for table and column metadata.
/// </summary>
public static class SchemaIntrospector
{
    /// <summary>
    /// Get all tables and their column information for the specified schema.
    /// </summary>
    public static TableSchema[] GetTables(NpgsqlConnection connection, string schemaName)
    {
        var tableNames = connection.Query<string>(
            """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = @schema AND table_type = 'BASE TABLE'
            ORDER BY table_name
            """,
            new { schema = schemaName }).ToArray();

        var result = new List<TableSchema>();
        foreach (var tableName in tableNames)
        {
            var columns = connection.Query<ColumnRaw>(
                """
                SELECT column_name, ordinal_position, udt_name, is_nullable, is_identity
                FROM information_schema.columns
                WHERE table_schema = @schema AND table_name = @table
                ORDER BY ordinal_position
                """,
                new { schema = schemaName, table = tableName })
                .Select(r => new ColumnInfo(
                    r.column_name,
                    r.ordinal_position,
                    r.udt_name,
                    string.Equals(r.is_nullable, "YES", StringComparison.OrdinalIgnoreCase),
                    !string.Equals(r.is_identity, "NO", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrEmpty(r.is_identity)))
                .ToArray();

            result.Add(new TableSchema(schemaName, tableName, columns));
        }

        return result.ToArray();
    }

    // Dapper mapping type for information_schema.columns rows
    private sealed class ColumnRaw
    {
        public string column_name { get; set; } = "";
        public int ordinal_position { get; set; }
        public string udt_name { get; set; } = "";
        public string is_nullable { get; set; } = "";
        public string is_identity { get; set; } = "";
    }
}
