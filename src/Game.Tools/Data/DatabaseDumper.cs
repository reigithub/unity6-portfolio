using System.Globalization;
using System.Text;
using Dapper;
using Npgsql;
using Spectre.Console;

namespace Game.Tools.Data;

/// <summary>
/// Dumps PostgreSQL database tables to TSV files using information_schema metadata.
/// </summary>
public class DatabaseDumper
{
    /// <summary>
    /// Dump database tables to TSV files.
    /// </summary>
    public void Dump(string connectionString, string outDir, bool userOnly, bool masterOnly)
    {
        Directory.CreateDirectory(outDir);

        var tables = new List<TableSchema>();

        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();

        if (!userOnly)
        {
            tables.AddRange(SchemaIntrospector.GetTables(connection, "Master"));
        }

        if (!masterOnly)
        {
            tables.AddRange(SchemaIntrospector.GetTables(connection, "User"));
        }

        if (tables.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No tables to dump.[/]");
            return;
        }

        int totalRows = 0;
        foreach (var table in tables)
        {
            var columns = table.Columns;
            var columnList = string.Join(", ", columns.Select(c => $"\"{c.ColumnName}\""));
            var sql = $"SELECT {columnList} FROM \"{table.SchemaName}\".\"{table.TableName}\"";

            IEnumerable<dynamic> rows;
            try
            {
                rows = connection.Query(sql);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"  [yellow]SKIP:[/] {table.SchemaName}.{table.TableName} - {ex.Message}");
                continue;
            }

            var rowList = rows.ToList();
            if (rowList.Count == 0)
            {
                AnsiConsole.MarkupLine($"  [yellow]SKIP:[/] {table.SchemaName}.{table.TableName} - no rows");
                continue;
            }

            var filePath = Path.Combine(outDir, $"{table.TableName}.tsv");
            WriteTsv(filePath, columns, rowList);

            AnsiConsole.MarkupLine($"  [green]OK:[/] {table.SchemaName}.{table.TableName} ({rowList.Count} rows)");
            totalRows += rowList.Count;
        }

        AnsiConsole.MarkupLine($"\n[green]Dump completed: {totalRows} total rows exported to {outDir}[/]");
    }

    private static void WriteTsv(string filePath, ColumnInfo[] columns, List<dynamic> rows)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine(string.Join("\t", columns.Select(c => c.ColumnName)));

        // Data rows
        foreach (var row in rows)
        {
            var dict = (IDictionary<string, object>)row;
            var values = columns.Select(c => FormatValue(dict.TryGetValue(c.ColumnName, out var v) ? v : null));
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
