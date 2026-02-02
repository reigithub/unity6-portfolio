using System.Reflection;
using Dapper;
using Game.Tools.Proto;
using MasterMemory;
using Npgsql;
using Spectre.Console;

namespace Game.Tools.Data;

/// <summary>
/// Seeds PostgreSQL database from TSV files.
/// </summary>
public class DatabaseSeeder
{
    private static readonly Type[] UserTableTypes =
    [
        typeof(Game.Server.Tables.UserInfo),
        typeof(Game.Server.Tables.UserScore),
    ];

    /// <summary>
    /// Seed database from TSV files.
    /// </summary>
    public void Seed(string connectionString, string tsvDir, string protoDir, bool userOnly, bool masterOnly)
    {
        var absoluteTsvDir = Path.GetFullPath(tsvDir);
        var masterTables = new List<(string Schema, string TableName, Type Type)>();
        var userTables = new List<(string Schema, string TableName, Type Type)>();

        // Collect Master tables from MemoryTable types
        if (!userOnly)
        {
            var assembly = typeof(Game.Server.MasterData.SurvivorPlayerMaster).Assembly;
            var memoryTableTypes = assembly.GetTypes()
                .Where(t => t.GetCustomAttribute<MemoryTableAttribute>() != null)
                .OrderBy(t => t.Name)
                .ToArray();

            foreach (var type in memoryTableTypes)
            {
                masterTables.Add(("Master", type.Name, type));
            }
        }

        // Collect User tables
        if (!masterOnly)
        {
            foreach (var type in UserTableTypes)
            {
                userTables.Add(("User", type.Name, type));
            }
        }

        var allTables = masterTables.Concat(userTables).ToList();
        if (allTables.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No tables to seed.[/]");
            return;
        }

        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();

        // TRUNCATE all tables with CASCADE
        AnsiConsole.MarkupLine("[blue]Truncating tables...[/]");
        var truncateList = allTables.Select(t => $"\"{t.Schema}\".\"{t.TableName}\"");
        var truncateSql = $"TRUNCATE TABLE {string.Join(", ", truncateList)} CASCADE";
        connection.Execute(truncateSql);
        AnsiConsole.MarkupLine("[green]Truncated all target tables.[/]");

        // INSERT: Master first, then User (FK constraint order)
        int totalRows = 0;
        var insertOrder = masterTables.Concat(userTables);

        foreach (var (schema, tableName, type) in insertOrder)
        {
            var tsvPath = Path.Combine(absoluteTsvDir, $"{tableName}.tsv");
            if (!File.Exists(tsvPath))
            {
                AnsiConsole.MarkupLine($"  [yellow]SKIP:[/] {schema}.{tableName} - TSV not found");
                continue;
            }

            var rows = TsvReader.ReadTsv(type, tsvPath);
            if (rows.Length == 0)
            {
                AnsiConsole.MarkupLine($"  [yellow]SKIP:[/] {schema}.{tableName} - no rows");
                continue;
            }

            var props = GetColumnProperties(type);
            var columns = string.Join(", ", props.Select(p => $"\"{p.Name}\""));
            var parameters = string.Join(", ", props.Select(p => $"@{p.Name}"));
            var sql = $"INSERT INTO \"{schema}\".\"{tableName}\" ({columns}) VALUES ({parameters})";

            // Convert object[] to DynamicParameters for Dapper
            foreach (var row in rows)
            {
                var dp = new DynamicParameters();
                foreach (var prop in props)
                {
                    dp.Add(prop.Name, prop.GetValue(row));
                }

                connection.Execute(sql, dp);
            }

            AnsiConsole.MarkupLine($"  [green]OK:[/] {schema}.{tableName} ({rows.Length} rows)");
            totalRows += rows.Length;
        }

        AnsiConsole.MarkupLine($"\n[green]Seed completed: {totalRows} total rows inserted.[/]");
    }

    /// <summary>
    /// Get properties that map to database columns (excluding navigation properties).
    /// </summary>
    internal static PropertyInfo[] GetColumnProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && IsColumnProperty(p))
            .ToArray();
    }

    /// <summary>
    /// Determine if a property is a database column (not a navigation property).
    /// Navigation properties are reference types that are not string or byte[].
    /// </summary>
    private static bool IsColumnProperty(PropertyInfo prop)
    {
        var propType = prop.PropertyType;

        // Nullable<T> — check the underlying type
        var underlying = Nullable.GetUnderlyingType(propType);
        if (underlying != null)
        {
            propType = underlying;
        }

        // string and byte[] are column types
        if (propType == typeof(string) || propType == typeof(byte[]))
        {
            return true;
        }

        // Value types (int, long, float, DateTime, etc.) are column types
        if (propType.IsValueType)
        {
            return true;
        }

        // Reference types (other classes) are navigation properties
        return false;
    }
}
