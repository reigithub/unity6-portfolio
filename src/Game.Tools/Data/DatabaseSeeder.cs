using Dapper;
using Npgsql;
using Spectre.Console;

namespace Game.Tools.Data;

/// <summary>
/// Seeds PostgreSQL database from TSV files using information_schema metadata.
/// </summary>
public class DatabaseSeeder
{
    /// <summary>
    /// Seed database from TSV files.
    /// </summary>
    public void Seed(string connectionString, string tsvDir, string[] schemas)
    {
        var absoluteTsvDir = Path.GetFullPath(tsvDir);

        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();

        var allTables = new List<TableSchema>();
        foreach (var schema in schemas)
        {
            allTables.AddRange(SchemaIntrospector.GetTables(connection, schema)
                .Where(t => t.TableName != "VersionInfo"));
        }

        if (allTables.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No tables to seed.[/]");
            return;
        }

        // TRUNCATE all tables with CASCADE
        AnsiConsole.MarkupLine("[blue]Truncating tables...[/]");
        var truncateList = allTables.Select(t => $"\"{t.SchemaName}\".\"{t.TableName}\"");
        var truncateSql = $"TRUNCATE TABLE {string.Join(", ", truncateList)} CASCADE";
        connection.Execute(truncateSql);
        AnsiConsole.MarkupLine("[green]Truncated all target tables.[/]");

        int totalRows = 0;

        foreach (var table in allTables)
        {
            var tsvPath = Path.Combine(absoluteTsvDir, $"{table.TableName}.tsv");
            if (!File.Exists(tsvPath))
            {
                AnsiConsole.MarkupLine($"  [yellow]SKIP:[/] {table.SchemaName}.{table.TableName} - TSV not found");
                continue;
            }

            var (headers, rows) = TsvReader.ReadTsvRaw(tsvPath);
            if (rows.Length == 0)
            {
                AnsiConsole.MarkupLine($"  [yellow]SKIP:[/] {table.SchemaName}.{table.TableName} - no rows");
                continue;
            }

            // Intersect TSV headers with DB columns, excluding identity columns
            var dbColumnMap = table.Columns.ToDictionary(c => c.ColumnName);
            var insertColumns = new List<(int TsvIndex, ColumnInfo Column)>();

            for (int i = 0; i < headers.Length; i++)
            {
                if (dbColumnMap.TryGetValue(headers[i], out var col) && !col.IsIdentity)
                {
                    insertColumns.Add((i, col));
                }
            }

            if (insertColumns.Count == 0)
            {
                AnsiConsole.MarkupLine($"  [yellow]SKIP:[/] {table.SchemaName}.{table.TableName} - no matching columns");
                continue;
            }

            var columnList = string.Join(", ", insertColumns.Select(c => $"\"{c.Column.ColumnName}\""));
            var paramList = string.Join(", ", insertColumns.Select(c => $"@{c.Column.ColumnName}"));
            var sql = $"INSERT INTO \"{table.SchemaName}\".\"{table.TableName}\" ({columnList}) VALUES ({paramList})";

            foreach (var row in rows)
            {
                var dp = new DynamicParameters();
                foreach (var (tsvIndex, column) in insertColumns)
                {
                    var rawValue = tsvIndex < row.Length ? row[tsvIndex] : "";
                    var value = TsvReader.ParseValueByUdtName(column.UdtName, rawValue, column.IsNullable);
                    dp.Add(column.ColumnName, value);
                }

                connection.Execute(sql, dp);
            }

            AnsiConsole.MarkupLine($"  [green]OK:[/] {table.SchemaName}.{table.TableName} ({rows.Length} rows)");
            totalRows += rows.Length;
        }

        AnsiConsole.MarkupLine($"\n[green]Seed completed: {totalRows} total rows inserted.[/]");
    }
}
