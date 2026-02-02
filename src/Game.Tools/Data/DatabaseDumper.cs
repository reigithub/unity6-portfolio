using System.Globalization;
using System.Reflection;
using System.Text;
using Dapper;
using MasterMemory;
using Npgsql;
using Spectre.Console;

namespace Game.Tools.Data;

/// <summary>
/// Dumps PostgreSQL database tables to TSV files.
/// </summary>
public class DatabaseDumper
{
    private static readonly Type[] UserTableTypes =
    [
        typeof(Game.Server.Tables.UserInfo),
        typeof(Game.Server.Tables.UserScore),
    ];

    /// <summary>
    /// Dump database tables to TSV files.
    /// </summary>
    public void Dump(string connectionString, string outDir, bool userOnly, bool masterOnly)
    {
        Directory.CreateDirectory(outDir);

        var tables = new List<(string Schema, string TableName, Type Type)>();

        // Collect Master tables
        if (!userOnly)
        {
            var assembly = typeof(Game.Server.MasterData.SurvivorPlayerMaster).Assembly;
            var memoryTableTypes = assembly.GetTypes()
                .Where(t => t.GetCustomAttribute<MemoryTableAttribute>() != null)
                .OrderBy(t => t.Name)
                .ToArray();

            foreach (var type in memoryTableTypes)
            {
                tables.Add(("Master", type.Name, type));
            }
        }

        // Collect User tables
        if (!masterOnly)
        {
            foreach (var type in UserTableTypes)
            {
                tables.Add(("User", type.Name, type));
            }
        }

        if (tables.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No tables to dump.[/]");
            return;
        }

        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();

        int totalRows = 0;
        foreach (var (schema, tableName, type) in tables)
        {
            var props = DatabaseSeeder.GetColumnProperties(type);
            var columns = string.Join(", ", props.Select(p => $"\"{p.Name}\""));
            var sql = $"SELECT {columns} FROM \"{schema}\".\"{tableName}\"";

            IEnumerable<dynamic> rows;
            try
            {
                rows = connection.Query(sql);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"  [yellow]SKIP:[/] {schema}.{tableName} - {ex.Message}");
                continue;
            }

            var rowList = rows.ToList();
            if (rowList.Count == 0)
            {
                AnsiConsole.MarkupLine($"  [yellow]SKIP:[/] {schema}.{tableName} - no rows");
                continue;
            }

            var filePath = Path.Combine(outDir, $"{tableName}.tsv");
            WriteTsv(filePath, props, rowList);

            AnsiConsole.MarkupLine($"  [green]OK:[/] {schema}.{tableName} ({rowList.Count} rows)");
            totalRows += rowList.Count;
        }

        AnsiConsole.MarkupLine($"\n[green]Dump completed: {totalRows} total rows exported to {outDir}[/]");
    }

    private static void WriteTsv(string filePath, PropertyInfo[] props, List<dynamic> rows)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine(string.Join("\t", props.Select(p => p.Name)));

        // Data rows
        foreach (var row in rows)
        {
            var dict = (IDictionary<string, object>)row;
            var values = props.Select(p => FormatValue(dict.TryGetValue(p.Name, out var v) ? v : null));
            sb.AppendLine(string.Join("\t", values));
        }

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }

    private static string FormatValue(object? value)
    {
        if (value == null || value == DBNull.Value)
        {
            return string.Empty;
        }

        return value switch
        {
            float f => f.ToString(CultureInfo.InvariantCulture),
            double d => d.ToString(CultureInfo.InvariantCulture),
            decimal m => m.ToString(CultureInfo.InvariantCulture),
            DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
            bool b => b ? "1" : "0",
            _ => value.ToString() ?? string.Empty,
        };
    }
}
