using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Spectre.Console;

namespace Game.Tools.Data;

/// <summary>
/// Exports MemoryDatabase tables to JSON or TSV format.
/// </summary>
public static class MasterDataExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Export all tables from MemoryDatabase to the specified format.
    /// </summary>
    public static void Export(object database, string format, string outDir)
    {
        Directory.CreateDirectory(outDir);

        var tableProps = database.GetType().GetProperties()
            .Where(p => p.Name.EndsWith("Table", StringComparison.Ordinal))
            .ToArray();

        int count = 0;
        foreach (var tableProp in tableProps)
        {
            var tableName = tableProp.Name.Replace("Table", string.Empty);
            var tableObj = tableProp.GetValue(database);
            if (tableObj == null)
            {
                continue;
            }

            // Get all rows via GetRawDataUnsafe() or fallback to IEnumerable
            var rows = GetRows(tableObj);
            if (rows == null || rows.Count == 0)
            {
                continue;
            }

            switch (format)
            {
                case "json":
                    ExportJson(tableName, rows, outDir);
                    break;
                case "tsv":
                    ExportTsv(tableName, rows, outDir);
                    break;
            }

            AnsiConsole.MarkupLine($"  [green]OK:[/] {tableName} ({rows.Count} rows)");
            count++;
        }

        AnsiConsole.MarkupLine($"[green]Exported {count} tables to {outDir}[/]");
    }

    private static List<object> GetRows(object tableObj)
    {
        var rows = new List<object>();

        // MemoryTable types expose GetRawDataUnsafe() that returns RawData
        var getRawMethod = tableObj.GetType().GetMethod("GetRawDataUnsafe");
        if (getRawMethod != null)
        {
            var rawData = getRawMethod.Invoke(tableObj, null);
            if (rawData is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    rows.Add(item);
                }
            }

            return rows;
        }

        // Fallback: try IEnumerable
        if (tableObj is IEnumerable fallbackEnumerable)
        {
            foreach (var item in fallbackEnumerable)
            {
                rows.Add(item);
            }
        }

        return rows;
    }

    private static void ExportJson(string tableName, List<object> rows, string outDir)
    {
        var filePath = Path.Combine(outDir, $"{tableName}.json");
        var json = JsonSerializer.Serialize(rows, JsonOptions);
        File.WriteAllText(filePath, json, Encoding.UTF8);
    }

    private static void ExportTsv(string tableName, List<object> rows, string outDir)
    {
        if (rows.Count == 0)
        {
            return;
        }

        var filePath = Path.Combine(outDir, $"{tableName}.tsv");
        var elementType = rows[0].GetType();
        var properties = elementType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToArray();

        var sb = new StringBuilder();

        // Header row: PascalCase property names
        sb.AppendLine(string.Join("\t", properties.Select(p => p.Name)));

        // Data rows
        foreach (var row in rows)
        {
            var values = properties.Select(p => FormatValue(p.GetValue(row)));
            sb.AppendLine(string.Join("\t", values));
        }

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }

    private static string FormatValue(object? value)
    {
        if (value == null)
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
