using Game.Tools.Data;
using Spectre.Console;

namespace Game.Tools.Commands;

public class SeedDataCommands
{
    /// <summary>
    /// Seed database tables from TSV files.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string. Falls back to appsettings.json if omitted.</param>
    /// <param name="tsvDir">Directory containing TSV files.</param>
    /// <param name="userOnly">Only seed User schema tables.</param>
    /// <param name="masterOnly">Only seed Master schema tables.</param>
    public void Seed(string connectionString = "", string tsvDir = "masterdata/raw/", bool userOnly = false, bool masterOnly = false)
    {
        if (userOnly && masterOnly)
        {
            AnsiConsole.MarkupLine("[red]Cannot specify both --user-only and --master-only.[/]");
            Environment.ExitCode = 1;
            return;
        }

        var cs = AppConfig.ResolveConnectionString(connectionString);

        AnsiConsole.MarkupLine($"[blue]TSV directory:[/] {Path.GetFullPath(tsvDir)}");
        AnsiConsole.MarkupLine($"[blue]Connection:[/] {MaskConnectionString(cs)}");

        var seeder = new DatabaseSeeder();
        seeder.Seed(cs, tsvDir, userOnly, masterOnly);
    }

    /// <summary>
    /// Dump database tables to TSV files.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string. Falls back to appsettings.json if omitted.</param>
    /// <param name="outDir">Output directory for TSV files.</param>
    /// <param name="userOnly">Only dump User schema tables.</param>
    /// <param name="masterOnly">Only dump Master schema tables.</param>
    public void Dump(string connectionString = "", string outDir = "output/dump/", bool userOnly = false, bool masterOnly = false)
    {
        if (userOnly && masterOnly)
        {
            AnsiConsole.MarkupLine("[red]Cannot specify both --user-only and --master-only.[/]");
            Environment.ExitCode = 1;
            return;
        }

        var cs = AppConfig.ResolveConnectionString(connectionString);

        AnsiConsole.MarkupLine($"[blue]Output directory:[/] {Path.GetFullPath(outDir)}");
        AnsiConsole.MarkupLine($"[blue]Connection:[/] {MaskConnectionString(cs)}");

        var dumper = new DatabaseDumper();
        dumper.Dump(cs, outDir, userOnly, masterOnly);
    }

    /// <summary>
    /// Compare two TSV directories to verify round-trip consistency (seed → dump).
    /// </summary>
    /// <param name="sourceDir">Source TSV directory (used for seed).</param>
    /// <param name="targetDir">Target TSV directory (produced by dump).</param>
    public void Diff(string sourceDir, string targetDir)
    {
        var sourcePath = Path.GetFullPath(sourceDir);
        var targetPath = Path.GetFullPath(targetDir);

        if (!Directory.Exists(sourcePath))
        {
            AnsiConsole.MarkupLine($"[red]Source directory not found:[/] {sourcePath}");
            Environment.ExitCode = 1;
            return;
        }

        if (!Directory.Exists(targetPath))
        {
            AnsiConsole.MarkupLine($"[red]Target directory not found:[/] {targetPath}");
            Environment.ExitCode = 1;
            return;
        }

        AnsiConsole.MarkupLine($"[blue]Source:[/] {sourcePath}");
        AnsiConsole.MarkupLine($"[blue]Target:[/] {targetPath}");
        AnsiConsole.WriteLine();

        var sourceFiles = Directory.GetFiles(sourcePath, "*.tsv").Select(f => Path.GetFileName(f)!).ToHashSet();
        var targetFiles = Directory.GetFiles(targetPath, "*.tsv").Select(f => Path.GetFileName(f)!).ToHashSet();

        var sourceOnly = sourceFiles.Except(targetFiles).Order().ToList();
        var targetOnly = targetFiles.Except(sourceFiles).Order().ToList();
        var commonFiles = sourceFiles.Intersect(targetFiles).Order().ToList();

        int matchCount = 0;
        int diffCount = 0;
        bool hasDifferences = false;

        // Report files only in source
        foreach (var file in sourceOnly)
        {
            AnsiConsole.MarkupLine($"[yellow]Source only:[/] {file}");
            hasDifferences = true;
        }

        // Report files only in target
        foreach (var file in targetOnly)
        {
            AnsiConsole.MarkupLine($"[yellow]Target only:[/] {file}");
            hasDifferences = true;
        }

        // Compare common files
        foreach (var file in commonFiles)
        {
            var sourceFilePath = Path.Combine(sourcePath, file);
            var targetFilePath = Path.Combine(targetPath, file);

            if (CompareFile(file, sourceFilePath, targetFilePath))
            {
                matchCount++;
            }
            else
            {
                diffCount++;
                hasDifferences = true;
            }
        }

        // Summary
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]--- Summary ---[/]");
        AnsiConsole.MarkupLine($"  Matched:     [green]{matchCount}[/]");
        if (diffCount > 0)
            AnsiConsole.MarkupLine($"  Differences: [red]{diffCount}[/]");
        else
            AnsiConsole.MarkupLine($"  Differences: {diffCount}");
        if (sourceOnly.Count > 0)
            AnsiConsole.MarkupLine($"  Source only:  [yellow]{sourceOnly.Count}[/]");
        if (targetOnly.Count > 0)
            AnsiConsole.MarkupLine($"  Target only:  [yellow]{targetOnly.Count}[/]");

        if (hasDifferences)
        {
            Environment.ExitCode = 1;
        }
        else
        {
            AnsiConsole.MarkupLine("[green]All files match.[/]");
        }
    }

    /// <summary>
    /// Compare a single TSV file between source and target. Returns true if matching.
    /// </summary>
    private static bool CompareFile(string fileName, string sourcePath, string targetPath)
    {
        var (sourceHeaders, sourceRows) = TsvReader.ReadTsvRaw(sourcePath);
        var (targetHeaders, targetRows) = TsvReader.ReadTsvRaw(targetPath);

        var sourceHeaderSet = sourceHeaders.ToHashSet();
        var targetHeaderSet = targetHeaders.ToHashSet();

        var commonColumns = sourceHeaders.Where(h => targetHeaderSet.Contains(h)).ToArray();
        var sourceOnlyColumns = sourceHeaders.Where(h => !targetHeaderSet.Contains(h)).ToArray();
        var targetOnlyColumns = targetHeaders.Where(h => !sourceHeaderSet.Contains(h)).ToArray();

        bool isMatch = true;

        // Report column differences
        if (sourceOnlyColumns.Length > 0)
        {
            AnsiConsole.MarkupLine($"[yellow]{fileName}:[/] Source-only columns: {string.Join(", ", sourceOnlyColumns)}");
            isMatch = false;
        }

        if (targetOnlyColumns.Length > 0)
        {
            AnsiConsole.MarkupLine($"[yellow]{fileName}:[/] Target-only columns: {string.Join(", ", targetOnlyColumns)}");
        }

        if (commonColumns.Length == 0)
        {
            AnsiConsole.MarkupLine($"[red]{fileName}:[/] No common columns to compare.");
            return false;
        }

        // Build column index maps
        var sourceIndexMap = new Dictionary<string, int>();
        for (int i = 0; i < sourceHeaders.Length; i++)
            sourceIndexMap[sourceHeaders[i]] = i;

        var targetIndexMap = new Dictionary<string, int>();
        for (int i = 0; i < targetHeaders.Length; i++)
            targetIndexMap[targetHeaders[i]] = i;

        // Compare row counts
        if (sourceRows.Length != targetRows.Length)
        {
            AnsiConsole.MarkupLine($"[red]{fileName}:[/] Row count mismatch — source: {sourceRows.Length}, target: {targetRows.Length}");
            isMatch = false;
        }

        // Compare cell values for common rows and columns
        int rowsToCompare = Math.Min(sourceRows.Length, targetRows.Length);
        int cellDiffCount = 0;
        const int maxCellDiffsToShow = 10;

        for (int row = 0; row < rowsToCompare; row++)
        {
            foreach (var col in commonColumns)
            {
                var sourceVal = sourceIndexMap[col] < sourceRows[row].Length ? sourceRows[row][sourceIndexMap[col]] : "";
                var targetVal = targetIndexMap[col] < targetRows[row].Length ? targetRows[row][targetIndexMap[col]] : "";

                if (sourceVal != targetVal)
                {
                    cellDiffCount++;
                    if (cellDiffCount <= maxCellDiffsToShow)
                    {
                        AnsiConsole.MarkupLine(
                            $"[red]{fileName}:[/] Row {row + 1}, Column [bold]{col}[/]: " +
                            $"[red]\"{Markup.Escape(sourceVal)}\"[/] → [green]\"{Markup.Escape(targetVal)}\"[/]");
                    }

                    isMatch = false;
                }
            }
        }

        if (cellDiffCount > maxCellDiffsToShow)
        {
            AnsiConsole.MarkupLine($"[red]{fileName}:[/] ... and {cellDiffCount - maxCellDiffsToShow} more cell difference(s).");
        }

        return isMatch;
    }

    private static string MaskConnectionString(string connectionString)
    {
        var parts = connectionString.Split(';');
        var masked = parts.Select(p =>
            p.TrimStart().StartsWith("Password", StringComparison.OrdinalIgnoreCase)
                ? "Password=***"
                : p);
        return string.Join(";", masked);
    }
}
